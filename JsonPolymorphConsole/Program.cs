// See https://aka.ms/new-console-template for more information
using JsonPolymorph;
using Newtonsoft.Json;
using SampleClassLibrary;

//You can white-list or black-list (nested) containers' inner types. Black-list has priority
var converter = new PolymorphJsonConverter(
	skipUnresolvedTypes: true
	//includedTypesOnly: new List<Type>() { typeof(IBar) },
	//	excludedTypes: new List<Type>() { typeof(IFoo) }
	);
JsonSerializerSettings settings = new JsonSerializerSettings()
{
	Formatting = Formatting.Indented,
	//this is disabled to show PolymorphJsonConverter. You can experiment with its abilities!
	//TypeNameHandling = TypeNameHandling.Auto
};
settings.Converters.Add(converter);

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

string polymorphJsonString = JsonConvert.SerializeObject(list, settings);
Console.WriteLine(polymorphJsonString);
var result = JsonConvert.DeserializeObject<IBar[]>(polymorphJsonString, settings);
Console.WriteLine();


List<IBar> list1 = new List<IBar>()
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
					//foosReversed = new Dictionary<IFoo, string>() {
					//	{ new A() {a = "i am key"}, "i am value of a" },
					//	{ new B() { b = 23412 }, "i am value of b" }
					//},
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

ComplexTest(10000);
void ComplexTest(int count)
{
	var converter = new PolymorphJsonConverter(
		skipUnresolvedTypes: false
	);
	JsonSerializerSettings settings = new JsonSerializerSettings()
	{
		Formatting = Formatting.Indented,
	};
	settings.Converters.Add(converter);
	var settingsDefault = new JsonSerializerSettings()
	{
		Formatting = Formatting.Indented,
		TypeNameHandling = TypeNameHandling.Auto
	};

	string polymorphResult = null, standardResult = null;
	var watch = new System.Diagnostics.Stopwatch();

	watch.Start();
	for (int i = 0; i < count; i++)
	{
		polymorphResult = JsonConvert.SerializeObject(list1, settings);
	}
	watch.Stop();
	Console.WriteLine($"JsonPolymorph {count} serialize execution time: {watch.ElapsedMilliseconds} ms");

	/*watch.Restart();
	for (int i = 0; i < count; i++)
	{
		standardResult = JsonConvert.SerializeObject(list1, settingsDefault);
	}
	watch.Stop();
	Console.WriteLine($"Default {count} serialize execution time: {watch.ElapsedMilliseconds} ms");*/

	watch.Restart();
	for (int i = 0; i < count; i++)
	{
		JsonConvert.DeserializeObject<IBar[]>(polymorphResult, settings);
	}
	watch.Stop();
	Console.WriteLine($"JsonPolymorph {count} DEserialize execution time: {watch.ElapsedMilliseconds} ms");

	/*watch.Restart();
	for (int i = 0; i < count; i++)
	{
		JsonConvert.DeserializeObject<IBar[]>(standardResult, settingsDefault);
	}
	watch.Stop();
	Console.WriteLine($"Default {count} DEserialize execution time: {watch.ElapsedMilliseconds} ms");*/
}