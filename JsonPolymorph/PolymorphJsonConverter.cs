using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace JsonPolymorph
{
	public class PolymorphJsonConverter : JsonConverter
	{
		readonly IEnumerable<Type> includedTypesOnly = null, excludedTypes = null;
		readonly bool skipUnresolvedTypes = true;

		public PolymorphJsonConverter()
		{
		}
		/// <param name="skipUnresolvedTypes">If type (custom or other) is generic but non-default-collection or in other special 
		/// cases the <paramref name="skipUnresolvedTypes"/> argument will decide will the exception be thrown or not</param>
		public PolymorphJsonConverter(bool skipUnresolvedTypes)
		{
			this.skipUnresolvedTypes = skipUnresolvedTypes;
		}
		/// <summary>
		/// Include or exclude types from (de)serialization. You can also use [Newtonsoft.JsonIgnore] attribute
		/// </summary>
		/// <param name="includedTypesOnly">Only these interface types will be resolved and (de)serialized</param>
		/// <param name="excludedTypes">These interface types will NOT be resolved and (de)serialized</param>
		public PolymorphJsonConverter(bool skipUnresolvedTypes = true, IEnumerable<Type> includedTypesOnly = null, IEnumerable<Type> excludedTypes = null)
			:this(skipUnresolvedTypes)
		{
			this.includedTypesOnly = includedTypesOnly;
			this.excludedTypes = excludedTypes;
		}

		static readonly string typeKey = "TypeFullName";
		public override bool CanConvert(Type objectType)
		{
			return TryGetContainerType(objectType, out _) || objectType.IsInterface;
		}

		bool InnerContainerTypeHandlable(Type innerType)
		{
			//not in included list
			if (includedTypesOnly != null && !includedTypesOnly.Any(x => x == innerType))
				return false;
			//in excluded list
			if (excludedTypes != null && excludedTypes.Any(x => x == innerType))
				return false;
			return true;
		}

		bool supportKvp = true, supportTuples = true;

		bool TryGetContainerType(Type objectType, out Type containerInnerType) 
		{
			containerInnerType = null;
			if(objectType == null)
				return false;
			if (objectType.IsArray)
			{
				containerInnerType = objectType.GetElementType()!;
				return containerInnerType!.IsInterface;
			}
			if (objectType.IsGenericType)
			{
				containerInnerType = objectType.GetInterfaces().FirstOrDefault(x => x.IsGenericType
					&& x.GetGenericTypeDefinition() == typeof(IEnumerable<>))?.GetGenericArguments().First()!;
				var result = containerInnerType?.IsInterface ?? false;
				if (!result && supportKvp)
				{
					if (containerInnerType.IsKeyValuePair())
					{
						(var kvpKeyType, var kvpValueType) = containerInnerType.GetKeyValuePairTypes();
						return kvpKeyType.IsInterface || kvpValueType.IsInterface;
					}
					if (!objectType.IsGenericType)
						return false;
					var genTypes = objectType.GetGenericArguments();
					if (supportTuples && genTypes.Any(x => x.IsValueTuple()))
						return true;
					result = genTypes?.Any(x => x.IsInterface) ?? false;
				}
				return result;
			}
			return false;
		}
		bool TryGetContainerType(object source, out Type containerInnerType) => TryGetContainerType(source.GetType(), out containerInnerType);

		public override bool CanWrite
		{
			get { return true; }
		}

		object CreateKvp(JArray tupleArray, Type innerType, JsonSerializer serializer)
		{
			(var keyType, var valueType) = innerType.GetKeyValuePairTypes();

			object key = null,
				value = null;
			if (keyType.IsJValueType())
				key = tupleArray[0].ToObject(keyType, serializer)!;
			else
			{
				if (keyType.IsInterface)
				{
					var splt = tupleArray[0][typeKey]!.ToString().Split('\\');
					string assemblyName = splt[0], typeFullName = splt[1];
					key = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
					serializer.Populate(tupleArray[0].CreateReader(), key);
				}
				else
					key = Activator.CreateInstance(keyType)!;
			}
			if (valueType.IsJValueType())
				value = tupleArray[1].ToObject(valueType, serializer)!;
			else
			{
				if (valueType.IsInterface)
				{
					var splt = tupleArray[1][typeKey]!.ToString().Split('\\');
					string assemblyName = splt[0], typeFullName = splt[1];
					value = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
				}
				else
					value = Activator.CreateInstance(valueType)!;
				serializer.Populate(tupleArray[1].CreateReader(), value);
			}
			var result = Activator.CreateInstance(innerType, key, value);
			return result;
		}

		object CreateTuple(JArray tupleArray, Type innerType, JsonSerializer serializer)
		{
			object[] args = new object[tupleArray.Count];
			var tupleTypes = innerType.GetTupleTypes();
			for (int i = 0; i < args.Length; i++)
			{
				if (tupleTypes[i].IsJValueType())
					args[i] = tupleArray[i].ToObject(tupleTypes[i], serializer)!;
				else
				{
					if (tupleTypes[i].IsInterface)
					{
						var splt = tupleArray[i][typeKey]!.ToString().Split('\\');
						string assemblyName = splt[0], typeFullName = splt[1];
						args[i] = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
					}
					else
						args[i] = Activator.CreateInstance(tupleTypes[i])!;
					serializer.Populate(tupleArray[i].CreateReader(), args[i]);
				}
			}
			var result = Activator.CreateInstance(innerType, args);
			return result;
		}

		void ReadDataToArray(JArray source, Array target, JsonSerializer serializer, Type innerType)
		{
			int i = 0;
			foreach (var element in source)
			{
				if (i == 0)
				{
					i++;
					continue;
				}
				object created = null!;

				if (element is JArray jArray)
				{
					if (innerType.IsKeyValuePair())
						created = CreateKvp(jArray, innerType, serializer);
					if (innerType.IsValueTuple())
						created = CreateTuple(jArray, innerType, serializer);
					target.SetValue(created, i++ - 1);
					continue;
				}
				
				var splt = element[typeKey]!.ToString().Split('\\');
				string assemblyName = splt[0], typeFullName = splt[1];
				try
				{
					created = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
				}
				catch (Exception e)
				{
					if (!skipUnresolvedTypes)
						throw new JsonPolymorphAnnotationException("JsonPolymporph container type defined in annotation not resolved inside " +
							"solution. You may be missing some libraries.");
					created = null!;
				}
				if (created != null)
				{
					try
					{
						serializer.Populate(element.CreateReader(), created);
					}
					catch (Exception e)
					{
						if (!skipUnresolvedTypes)
							throw new JsonPolymorphTypeException("Error handling inner collection. Some collections may be not supported even by Newtonsoft.Json", e);
					}
				}
				target.SetValue(created, i++ - 1);
			}
		}

		bool TryCreateContainerType(JArray jarray, JsonSerializer serializer, out Type containerType, out Type innerType, out Array arr)
		{
			arr = null!;
			innerType = null!; containerType = null!;
			//we just need first element but who cares
			foreach (var element in jarray)
			{
				//first element is TypeAnnotation for array initialization
				var typeAnnotation = new TypeAnnotation();
				serializer.Populate(element.CreateReader(), typeAnnotation);
				innerType = typeAnnotation.LoadTypeFromAssembly(skipUnresolvedTypes);
				//failed to load types
				if(innerType == null)
					return false;
				arr = Array.CreateInstance(innerType, jarray.Count - 1);

				try
				{
					containerType = Assembly.Load(typeAnnotation.containerAsmName).GetType(typeAnnotation.containerType)!;
				}
				catch (Exception e)
				{
					if (!skipUnresolvedTypes)
						if (typeAnnotation == null)
							throw new JsonPolymorphAnnotationException("JsonPolymporph annotation not found! Cannot resolve types!", e);
						else
							throw new JsonPolymorphAnnotationException("JsonPolymporph container type defined in annotation not resolved inside " +
								"solution. You may be missing some libraries.");
				}
				return InnerContainerTypeHandlable(innerType);
			}
			return false;
		}

		public override object ReadJson(JsonReader reader,
										Type objectType,
										 object existingValue,
										 JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;
			//non-collection object
			if (!TryGetContainerType(objectType, out _))
			{
				var element = JObject.Load(reader);
				var splt = element[typeKey]!.ToString().Split('\\');
				string assemblyName = splt[0], typeFullName = splt[1];
				object created = null;
				try
				{
					created = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
					serializer.Populate(reader, created);
				}
				catch (Exception e)
				{
					if (!skipUnresolvedTypes)
						throw new JsonPolymorphAnnotationException("JsonPolymporph container type defined in annotation not resolved inside " +
							"solution. You may be missing some libraries.");
					created = null!;
				}
				return created;
			}
			// Load JObject from stream
			JArray root = JArray.Load(reader);
			
			Array arr = null;
			bool useGenericType = false;
			Type containerType = null, innerType = null;
			
			TryCreateContainerType(root, serializer, out containerType, out innerType, out arr);
			useGenericType = !containerType.IsArray && containerType.IsGenericType;
			ReadDataToArray(root, arr, serializer, innerType);
			//convert array to generic
			if (!objectType.IsArray && objectType.IsGenericType && useGenericType)
			{
				if (innerType.IsKeyValuePair() && containerType.GetGenericArguments().Length == 2)
				{
					var genericTypes = innerType.GetKeyValuePairTypes();
					containerType = containerType.MakeGenericType(genericTypes.Item1, genericTypes.Item2);
				}
				else if (innerType.IsValueTuple())
					containerType = containerType.MakeGenericType(innerType);
				else
					containerType = containerType.MakeGenericType(innerType);
				try
				{
					return Activator.CreateInstance(containerType, arr)!;
				}
				catch(Exception e)
				{
					if (!skipUnresolvedTypes)
						throw new JsonPolymorphAnnotationException("Cannot create the instance of container. The container class may " +
							"be missing or dll ref unresolved", e);
					return null!;
				}
			}
			return arr;
		}

		class TypeAnnotation
		{
			public string innerAsmName, innerTypeName, containerAsmName, containerType;

			public Type LoadTypeFromAssembly(bool skipUnresolvedTypes)
			{
				try
				{
					return Assembly.Load(innerAsmName).GetType(innerTypeName)!;
				}
				catch (Exception e)
				{
					if(!skipUnresolvedTypes)
						throw new JsonPolymorphAnnotationException("Type defined in annotation can not be loaded! You may be missing dlls.", e);
					return null;
				}
			}
		}

		JArray CreateJArrayFromKvp(object? value, JsonSerializer serializer)
		{
			var vt = value.GetType();

			(var kvpKey, var kvpValue) = vt.GetKeyValuePairValues(value);
			(var kvpKeyType, var kvpValueType) = vt.GetKeyValuePairTypes();

			JArray kvpJArr = new JArray();
			//if is kind-of primitive type, use JValue
			if (kvpKeyType.IsJValueType())
				kvpJArr.Add(JValue.FromObject(kvpKey, serializer));
			else //otherwise this should be serialized via serializer
			{
				JObject joKey = JObject.FromObject(kvpKey, serializer);
				//if key is interface add annotation
				if (InnerContainerTypeHandlable(kvpKeyType) && kvpKeyType.IsInterface)
				{
					var kvpKeyRealType = kvpKey.GetType();
					joKey.Add(typeKey, $"{kvpKeyRealType.Assembly.FullName}\\{kvpKeyRealType.FullName}");
				}
				kvpJArr.Add(joKey);
			}

			if (kvpValueType != null && kvpValueType.IsJValueType())
				kvpJArr.Add(new JValue(kvpValue));
			else
			{
				//anyway add value
				JObject joValue = JObject.FromObject(kvpValue, serializer);
				//if value is interface add annotation
				if (kvpValueType != null && InnerContainerTypeHandlable(kvpValueType))
				{
					var kvpValueRealType = kvpValue.GetType();
					joValue.Add(typeKey, $"{kvpValueRealType.Assembly.FullName}\\{kvpValueRealType.FullName}");
				}
				kvpJArr.Add(joValue);
			}
			return kvpJArr;
		}

		JArray CreateJArrayFromTuple(object? value, JsonSerializer serializer)
		{
			var vt = value.GetType();
			JArray tupleJArr = new JArray();
			int i = 0;
			var tupleTypes = vt.GetTupleTypes();
			foreach (var tupleVal in vt.GetTupleValues(value))
			{
				if (tupleTypes[i].IsJValueType())
					tupleJArr.Add(JToken.FromObject(tupleVal, serializer));
				else
				{
					JObject joKey = JObject.FromObject(tupleVal, serializer);
					if (InnerContainerTypeHandlable(tupleTypes[i]) && tupleTypes[i].IsInterface)
					{
						var kvpKeyRealType = tupleVal.GetType();
						joKey.Add(typeKey, $"{kvpKeyRealType.Assembly.FullName}\\{kvpKeyRealType.FullName}");
					}
					tupleJArr.Add(joKey);
				}
				i++;
			}
			return tupleJArr;
		}

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			if (value is not IEnumerable enumerable)
			{
				var vt = value.GetType();
				if (vt.IsGenericType)
				{
					if (vt.IsValueTuple())
					{
						CreateJArrayFromTuple(value, serializer).WriteTo(writer);
						return;
					}
					if (vt.IsKeyValuePair())
					{
						CreateJArrayFromKvp(value, serializer).WriteTo(writer);
						return;
					}
				}
				JObject jo = JObject.FromObject(value, serializer);
				var type = value.GetType();
				jo.Add(typeKey, $"{type.Assembly.FullName}\\{type.FullName}");
				jo.WriteTo(writer);
				//throw new JsonPolymorphTypeException("Cannot cast container type to IEnumerable, KeyValuePair and Tuple!");
				return;
			}
			JArray array = new JArray();
			if (TryGetContainerType(value, out var containerInnerType))
				if (InnerContainerTypeHandlable(containerInnerType))
					array.Add(JObject.FromObject(new TypeAnnotation()
					{
						innerAsmName = containerInnerType.Assembly.FullName!,
						innerTypeName = containerInnerType.FullName!,
						containerAsmName = value.GetType().Assembly.FullName!,
						containerType = value.GetType().IsGenericType ? value.GetType().GetGenericTypeDefinition().FullName! : value.GetType().FullName!
					}));
				else return;
			else
				throw new JsonPolymorphTypeException("Type mismatch: serialized value is not a container!"); 

			foreach (var i in enumerable)
			{
				Type type = i.GetType();
				if (type.IsGenericType)
				{
					if (type.IsValueTuple())
						array.Add(CreateJArrayFromTuple(i, serializer));
					if (type.IsKeyValuePair())
						array.Add(CreateJArrayFromKvp(i, serializer));
				}
				else
				{
					JObject jo = JObject.FromObject(i, serializer);
					jo.Add(typeKey, $"{type.Assembly.FullName}\\{type.FullName}");
					array.Add(jo);
				}
			}
			array.WriteTo(writer);
		}
	}
}