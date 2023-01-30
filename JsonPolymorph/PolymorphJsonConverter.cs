using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

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
		public PolymorphJsonConverter(bool skipUnresolvedTypes = true, IEnumerable < Type> includedTypesOnly = null, IEnumerable<Type> excludedTypes = null)
			:this(skipUnresolvedTypes)
		{
			this.includedTypesOnly = includedTypesOnly;
			this.excludedTypes = excludedTypes;
		}

		static readonly string typeKey = "TypeFullName";
		public override bool CanConvert(Type objectType)
		{
			return TryGetContainerType(objectType, out var inner);
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
				return containerInnerType?.IsInterface ?? false;
			}
			return false;
		}
		bool TryGetContainerType(object source, out Type containerInnerType) => TryGetContainerType(source.GetType(), out containerInnerType);

		public override bool CanWrite
		{
			get { return true; }
		}

		//TODO add support for tuples there and here (need to create them as well)
		void ReadDataToArray(JArray source, Array target, JsonSerializer serializer)
		{
			int i = 0;
			foreach (var element in source)
			{
				if (i == 0)
				{
					i++;
					continue;
				}
				var splt = element[typeKey]!.ToString().Split('\\');
				string assemblyName = splt[0], typeFullName = splt[1];

				object created = null!;
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
			// Load JObject from stream
			JArray root = JArray.Load(reader);
			
			Array arr = null;
			bool useGenericType = false;
			Type containerType = null, innerType = null;
			
			TryCreateContainerType(root, serializer, out containerType, out innerType, out arr);
			useGenericType = !containerType.IsArray && containerType.IsGenericType;
			ReadDataToArray(root, arr, serializer);
			//convert array to generic
			if (!objectType.IsArray && objectType.IsGenericType && useGenericType)
			{
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

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			if (value is not IEnumerable enumerable)
				throw new JsonPolymorphTypeException("Cannot cast container type to IEnumerable!");
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
				JObject jo = JObject.FromObject(i, serializer);
				jo.Add(typeKey, $"{type.Assembly.FullName}\\{type.FullName}");
				array.Add(jo);
			}
			array.WriteTo(writer);
		}
	}
}