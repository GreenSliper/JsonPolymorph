# JsonPolymorph
This project is an add-on for Newtonsoft.JSON. Simple JsonConverter allows you to (de)serialize interface collections like List\<ICloneable>.

# Requirements
.Net 6.0 (earlier versions not tested, may be compatible but without null-forgiving operator)
Newtonsoft.JSON package

# Example
JsonPolymorphConsole contains an example in Program.cs. It shows how you can serialize and deserialize interface collections and arrays.

# Idea
PolymorphJsonConverter adds to json assembly and type names for interface-referenced objects. During deserialization, assemblies and types are loaded and instantiated via Activator class.
Converter does not affect any other Newtonsoft.JSON attributes (you can test it with JsonIgnoreAttribute, for example).

# Limitations
PolymorphJsonConverter is able to convert arrays, lists, and other similar collections. Unfortunately, the support for ValueTuples and so Dictionary type is coming soon, but not yet available at the moment.
