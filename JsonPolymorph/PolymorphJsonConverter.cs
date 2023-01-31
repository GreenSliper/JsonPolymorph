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
		readonly bool skipUnresolvedTypes = true, useCaches = true;

		public PolymorphJsonConverter()
		{
		}
		/// <param name="skipUnresolvedTypes">If type (custom or other) is generic but non-default-collection or in other special 
		/// cases the <paramref name="skipUnresolvedTypes"/> argument will decide will the exception be thrown or not</param>
		public PolymorphJsonConverter(bool skipUnresolvedTypes, bool useCaches)
		{
			this.skipUnresolvedTypes = skipUnresolvedTypes;
			this.useCaches = useCaches;
		}
		/// <summary>
		/// Include or exclude types from (de)serialization. You can also use [Newtonsoft.JsonIgnore] attribute
		/// </summary>
		/// <param name="includedTypesOnly">Only these interface types will be resolved and (de)serialized</param>
		/// <param name="excludedTypes">These interface types will NOT be resolved and (de)serialized</param>
		public PolymorphJsonConverter(bool skipUnresolvedTypes = true, bool useCaches = true, 
			IEnumerable<Type> includedTypesOnly = null, IEnumerable<Type> excludedTypes = null)
			: this(skipUnresolvedTypes, useCaches)
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

		Dictionary<Type, Type> containersInnerTypes = new Dictionary<Type, Type>();

		bool _TryGetContainerType(Type objectType, out Type containerInnerType)
		{
			containerInnerType = null;
			if (objectType == null)
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

		bool TryGetContainerType(Type objectType, out Type containerInnerType)
		{
			if (useCaches && containersInnerTypes.ContainsKey(objectType))
			{
				containerInnerType = containersInnerTypes[objectType];
				return containerInnerType != null;
			}
			var result = _TryGetContainerType(objectType, out containerInnerType);
			if (result)
				containersInnerTypes.Add(objectType, containerInnerType);
			else
				containersInnerTypes.Add(objectType, null);
			return result;
		}
		bool TryGetContainerType(object source, out Type containerInnerType) 
			=> TryGetContainerType(source.GetType(), out containerInnerType);

		public override bool CanWrite
		{
			get { return true; }
		}
		
		Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
		Assembly GetAssembly(string name)
		{
			if (useCaches && assemblies.TryGetValue(name, out var val))
				return val;
			val = Assembly.Load(name);
			if(useCaches)
				assemblies.Add(name, val);
			return val;
		}

		Dictionary<(string, Assembly), Type> assemblyTypes = new Dictionary<(string, Assembly), Type>();

		Type GetAssemblyType(Assembly asm, string type)
		{
			if (useCaches && assemblyTypes.TryGetValue((type, asm), out var val))
				return val;
			val = asm.GetType(type);
			if (useCaches)
				assemblyTypes.Add((type, asm), val);
			return val;
		}

		object CreateInnerTypeObject(Type type, JToken jObject, JsonSerializer serializer)
		{
			object result = null;
			if (type.IsJValueType())
				result = jObject.ToObject(type, serializer)!;
			else
			{
				if (type.IsInterface)
				{
					var splt = jObject[typeKey]!.ToString().Split('\\');
					string assemblyName = splt[0], typeFullName = splt[1];
					result = Activator.CreateInstance(GetAssemblyType(GetAssembly(assemblyName), typeFullName)!)!;
				}
				else
					result = Activator.CreateInstance(type)!;
				serializer.Populate(jObject.CreateReader(), result);
			}
			return result;
		}

		object CreateKvp(JArray tupleArray, Type innerType, JsonSerializer serializer)
		{
			(var keyType, var valueType) = innerType.GetKeyValuePairTypes();

			object key = CreateInnerTypeObject(keyType, tupleArray[0], serializer),
				value = CreateInnerTypeObject(valueType, tupleArray[1], serializer);
			var result = Activator.CreateInstance(innerType, key, value);
			return result;
		}

		object CreateTuple(JArray tupleArray, Type innerType, JsonSerializer serializer)
		{
			object[] args = new object[tupleArray.Count];
			var tupleTypes = innerType.GetTupleTypes();
			for (int i = 0; i < args.Length; i++)
				args[i] = CreateInnerTypeObject(tupleTypes[i], tupleArray[i], serializer);
			return Activator.CreateInstance(innerType, args)!;
		}

		void ReadDataToArray(JArray source, Array target, JsonSerializer serializer, Type innerType)
		{
			int i = 0;
			foreach (var element in source.Skip(1))
			{
				object created = null!;

				if (element is JArray jArray)
				{
					//array can be either kvp or valueTuple with various arguments, exciting, deserialize them
					if (innerType.IsKeyValuePair())
						created = CreateKvp(jArray, innerType, serializer);
					if (innerType.IsValueTuple())
						created = CreateTuple(jArray, innerType, serializer);
					target.SetValue(created, i++);
					continue;
				}

				var splt = element[typeKey]!.ToString().Split('\\');
				string assemblyName = splt[0], typeFullName = splt[1];
				try
				{
					created = Activator.CreateInstance(GetAssemblyType(GetAssembly(assemblyName), typeFullName)!)!;
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
				target.SetValue(created, i++);
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
				innerType = GetAssemblyType(GetAssembly(typeAnnotation.innerAsmName), typeAnnotation.innerTypeName);
				//failed to load types
				if (innerType == null)
					return false;
				arr = Array.CreateInstance(innerType, jarray.Count - 1);

				try
				{
					containerType = GetAssemblyType(GetAssembly(typeAnnotation.containerAsmName), typeAnnotation.containerType)!;
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
				if (!skipUnresolvedTypes)
					throw new JsonPolymorphAnnotationException("JsonPolymporph container type defined in annotation not resolved inside " +
						"solution. You may be missing some libraries.");
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
				var realContainerType = containerType;
				if (innerType.IsKeyValuePair() && containerType.GetGenericArguments().Length == 2)
				{
					var genericTypes = innerType.GetKeyValuePairTypes();
					realContainerType = containerType.MakeGenericType(genericTypes.Item1, genericTypes.Item2);
				}
				else if (innerType.IsValueTuple())
					realContainerType = containerType.MakeGenericType(innerType);
				else
					realContainerType = containerType.MakeGenericType(innerType);
				try
				{
					return Activator.CreateInstance(realContainerType, arr)!;
				}
				catch (Exception e)
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
		}

		JToken CreateJTokenFromType(Type type, object value, JsonSerializer serializer)
		{
			if (type.IsJValueType())
				return JToken.FromObject(value, serializer);
			else
			{
				JObject joKey = JObject.FromObject(value, serializer);
				if (InnerContainerTypeHandlable(type) && type.IsInterface)
				{
					var kvpKeyRealType = value.GetType();
					joKey.Add(typeKey, $"{kvpKeyRealType.Assembly.FullName}\\{kvpKeyRealType.FullName}");
				}
				return joKey;
			}
		}

		JArray CreateJArrayFromKvp(object? value, JsonSerializer serializer)
		{
			var vt = value.GetType();

			(var kvpKey, var kvpValue) = vt.GetKeyValuePairValues(value);
			(var kvpKeyType, var kvpValueType) = vt.GetKeyValuePairTypes();

			JArray kvpJArr = new JArray
			{
				CreateJTokenFromType(kvpKeyType, kvpKey, serializer),
				CreateJTokenFromType(kvpValueType, kvpValue, serializer)
			};
			return kvpJArr;
		}

		JArray CreateJArrayFromTuple(object? value, JsonSerializer serializer)
		{
			JArray tupleJArr = new JArray();
			var vt = value.GetType();
			int i = 0;
			var tupleTypes = vt.GetTupleTypes();
			foreach (var tupleVal in vt.GetTupleValues(value))
				tupleJArr.Add(CreateJTokenFromType(tupleTypes[i++], tupleVal, serializer));
			return tupleJArr;
		}

		JToken CreateCustomJToken(object obj, JsonSerializer serializer, out Type type)
		{
			type = obj.GetType();
			if (type.IsGenericType)
			{
				if (type.IsValueTuple())
					return CreateJArrayFromTuple(obj, serializer);
				if (type.IsKeyValuePair())
					return CreateJArrayFromKvp(obj, serializer);
			}
			return null!;
		}

		JObject GetContainerAnnotationTypeObject(object? value)
		{
			if (TryGetContainerType(value, out var containerInnerType))
				if (InnerContainerTypeHandlable(containerInnerType))
					return JObject.FromObject(new TypeAnnotation()
					{
						innerAsmName = containerInnerType.Assembly.FullName!,
						innerTypeName = containerInnerType.FullName!,
						containerAsmName = value.GetType().Assembly.FullName!,
						containerType = value.GetType().IsGenericType ? 
							value.GetType().GetGenericTypeDefinition().FullName! : value.GetType().FullName!
					});
				//leave container without annotation
				else return null;
			else
				throw new JsonPolymorphTypeException("Type mismatch: serialized value is not a container!");
		}

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			if (value is IEnumerable enumerable)
			{
				JArray array = new JArray();
				JObject jo = null;
				if ((jo = GetContainerAnnotationTypeObject(value)) != null)
					array.Add(jo);
				foreach (var i in enumerable)
				{
					JToken jt = null;
					if ((jt = CreateCustomJToken(i, serializer, out var type)) != null)
						array.Add(jt);
					else
					{
						JObject elemJo = JObject.FromObject(i, serializer);
						elemJo.Add(typeKey, $"{type.Assembly.FullName}\\{type.FullName}");
						array.Add(elemJo);
					}
				}
				array.WriteTo(writer);
			}
			else
			{
				JToken jt = null;
				if ((jt = CreateCustomJToken(value, serializer, out var type)) != null)
					jt.WriteTo(writer);
				else 
					throw new JsonPolymorphTypeException("Cannot cast container type to IEnumerable, KeyValuePair and Tuple!");
			}
		}

		public void ClearCaches()
		{
			if (!useCaches)
				return;
			containersInnerTypes.Clear();
			assemblies.Clear();
		}
	}
}