using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JsonPolymorph
{
	public class JsonSingleTypeConverter<T> : JsonConverter
	{
		static readonly string typeKey = "TypeFullName";
		bool skipUnresolvedTypes = true;
		public JsonSingleTypeConverter() { }
		public JsonSingleTypeConverter(bool skipUnresolvedTypes)
		{
			this.skipUnresolvedTypes = skipUnresolvedTypes;
		}
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(T);
		}

		public override bool CanWrite => true;
		public override bool CanRead => true;

		public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
		{
			var element = JObject.Load(reader);
			var splt = element[typeKey]!.ToString().Split('\\');
			string assemblyName = splt[0], typeFullName = splt[1];
			object created = null;
			try
			{
				created = Activator.CreateInstance(Assembly.Load(assemblyName).GetType(typeFullName)!)!;
				serializer.Populate(element.CreateReader(), created);
			}
			catch (Exception e)
			{
				if (!skipUnresolvedTypes)
					throw new JsonPolymorphAnnotationException("JsonPolymporph container type defined in annotation not resolved inside " +
						"solution. You may be missing some libraries.", e);
				created = null!;
			}
			return created;
		}

		public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
		{
			JObject jo = JObject.FromObject(value);
			var type = value.GetType();
			jo.Add(typeKey, $"{type.Assembly.FullName}\\{type.FullName}");
			jo.WriteTo(writer);
		}
	}
}
