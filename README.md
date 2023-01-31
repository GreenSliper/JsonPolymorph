# JsonPolymorph
This project is an add-on for Newtonsoft.JSON. Simple JsonConverter allows you to (de)serialize interface collections like List\<ICloneable>. In current condition it's a bit more flexible than default Newtonsoft.JSON TypeNameHandling.Auto setting, while this converter is able to deal with Dictionary<Interface, T> at least.

# Requirements
- .Net 6.0 (earlier versions not tested, may be compatible but without null-forgiving operator)
- Newtonsoft.JSON package

# Example
JsonPolymorphConsole contains an example in Program.cs. It shows how you can serialize and deserialize interface collections and arrays.
The example also uses the simple JsonSingleTypeConverter in JsonConverterAttribute (from Newtonsoft.JSON). It can be used this way:

```cs
[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
public IFoo Foo { get; set; }
[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
public IFoo _foo;
```
# Idea
PolymorphJsonConverter adds to json assembly and type names for interface-referenced objects. During deserialization, assemblies and types are loaded and instantiated via Activator class.

Added custom behaviour for Tuple and KeyValuePair classes (some generic types may be working as well).

Converter does not affect any other Newtonsoft.JSON attributes (you can test it with JsonIgnoreAttribute, for example).
