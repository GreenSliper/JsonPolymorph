# JsonPolymorph
This project is an add-on for Newtonsoft.JSON. Simple JsonConverter allows you to (de)serialize interface collections like List\<ICloneable>. In current condition it's a bit more flexible than default Newtonsoft.JSON TypeNameHandling.Auto setting, while this converter is able to deal with Dictionary<Interface, T> at least.

# Requirements
- .Net 6.0 (earlier versions not tested, may be compatible but without null-forgiving operator)
- Newtonsoft.JSON package

# Idea
PolymorphJsonConverter adds to json assembly and type names for interface-referenced objects. During deserialization, assemblies and types are loaded and instantiated via Activator class.

Added custom behaviour for Tuple and KeyValuePair classes (some generic types may be working as well).

Converter does not affect any other Newtonsoft.JSON attributes (you can test it with JsonIgnoreAttribute, for example).

# Tests
Solution contains xUnit tests in separate project.

# Example
JsonPolymorphConsole contains an example in Program.cs. It shows how you can serialize and deserialize interface collections and arrays.
The example also uses the simple JsonSingleTypeConverter in JsonConverterAttribute (from Newtonsoft.JSON). It can be used this way:

```cs
[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
public IFoo Foo { get; set; }
[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
public IFoo _foo;
```
We can use this structure for testing purposes:

IBar.cs:
```cs
	public interface IBar { }

	public interface IFoo { }

	public class A : IFoo {
		public string a;
	}

	public class B : IFoo {
		public int b;
	}

	public class C : IBar {
		public int a = 12;
	}

	public class D : IBar
	{
		public string b = "sample data";
		[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
		public IFoo Foo;//{ get; set; }
		[JsonIgnore]
		public string IgnoredString { get; set; }
		public Dictionary<string, string> dictTest = new Dictionary<string, string>() { { "1", "a" }, { "2", "b" } };
	}

	public class E : IBar
	{
		public IBar[] bars;
		public Dictionary<string, IFoo> foos;
		public Dictionary<IFoo, string> foosReversed;
		public List<KeyValuePair<string, IFoo>> keyValuePairTest;
		public List<(IFoo, int)> tupleTest;
	}
```

In Program.cs we have this mess:
```cs
//complex dynamic structure to serialize
var list = new List<IBar>()
{
	new C(),
	new D() { Foo = new A() { a = "Single interface"} },
	new E() 
	{ 
		bars = new IBar[2] { 
			new C() { a = 99 },
			new D() { b = "dynamic data", IgnoredString = "IGNORED DATA", Foo = new A() { a = "Interface field" } } 
		},
		foos = new Dictionary<string, IFoo>() {
			{ "1", new A() {a = "123456789"} },
			{ "2", new B() { b = 999999 } } 
		},
		foosReversed = new Dictionary<IFoo, string>() {
			{ new A() {a = "i am key"}, "i am value of a" },
			{ new B() { b = 23412 }, "i am value of b" }
		},
		keyValuePairTest = new List<KeyValuePair<string, IFoo>>() {
			new KeyValuePair<string, IFoo>("i am key of a", new A() {a = "i am key"}),
			new KeyValuePair<string, IFoo>("i am key of b", new B() { b = 23412 })
		},
		tupleTest = new List<(IFoo, int)>() {
			(new A() { a = "tuple test" }, 1),
			(new B() { b = 1111111 }, 2)
		}
	}
};
```
To serialize this we need to create converter and add it to settings:
```cs
var converter = new PolymorphJsonConverter();
JsonSerializerSettings settings = new JsonSerializerSettings()
{ Formatting = Formatting.Indented };
settings.Converters.Add(converter);
```
That's it! This structure will be serialized to JSON string easily:
```cs
string polymorphJsonString = JsonConvert.SerializeObject(list, settings);
Console.WriteLine(polymorphJsonString);
var result = JsonConvert.DeserializeObject<IBar[]>(polymorphJsonString, settings);
```
