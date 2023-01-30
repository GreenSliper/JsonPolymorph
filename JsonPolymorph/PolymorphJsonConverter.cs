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

		/// <summary>
		/// Include or exclude types from (de)serialization. You can also use [Newtonsoft.JsonIgnore] attribute
		/// </summary>
		/// <param name="includedTypesOnly">Only these interface types will be resolved and (de)serialized</param>
		/// <param name="excludedTypes">These interface types will NOT be resolved and (de)serialized</param>
		public PolymorphJsonConverter(IEnumerable<Type> includedTypesOnly = null, IEnumerable<Type> excludedTypes = null)
		{
			this.includedTypesOnly = includedTypesOnly;
			this.excludedTypes = excludedTypes;
		}

		static readonly string typeKey = "TypeFullName";
		public override bool CanConvert(Type objectType)
		{
			return TryGetContainerType(objectType, out _);
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
		bool TryGetContainerType(object source, out Type containerInnerType)=>TryGetContainerType(source.GetType(), out containerInnerType);

		public override bool CanWrite
		{
			get { return true; }
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
			//create array of target type
			Array arr = null;
			int i = 0;
			bool useGenericType = false;
			Type containerType = null, innerType = null;
			foreach (var element in root)
			{
				//first element is TypeAnnotation for array initialization
				if (i == 0 && arr == null)
				{
					var typeAnnotation = new TypeAnnotation();
					serializer.Populate(element.CreateReader(), typeAnnotation);
					//TODO add try catch for errors in loading assemblies?
					innerType = typeAnnotation.LoadTypeFromAssembly();
					arr = Array.CreateInstance(innerType, root.Count - 1);

					containerType = Assembly.Load(typeAnnotation.containerAsmName).GetType(typeAnnotation.containerType);
					
					if (!InnerContainerTypeHandlable(innerType))
						return null;
					useGenericType = !containerType.IsArray && containerType.IsGenericType;
					continue;
				}
				var splt = element[typeKey]!.ToString().Split('\\');
				string assemblyName = splt[0], typeFullName = splt[1];
				//TODO add try catch for errors in loading assemblies?
				var created = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
				serializer.Populate(element.CreateReader(), created);
				arr.SetValue(created, i++);
			}
			if (!objectType.IsArray && objectType.IsGenericType && useGenericType)
			{
				containerType = containerType.MakeGenericType(innerType);
				return Activator.CreateInstance(containerType, arr);
			}
			return arr;
		}

		class TypeAnnotation
		{
			public string innerAsmName, innerTypeName, containerAsmName, containerType;

			public Type LoadTypeFromAssembly()
			{
				return Assembly.Load(innerAsmName).GetType(innerTypeName);
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