// See https://aka.ms/new-console-template for more information
using JsonPolymorph;
using Newtonsoft.Json;
using SampleClassLibrary;

//You can white-list or black-list (nested) containers' inner types. Black-list has priority
var converter = new PolymorphJsonConverter(
	//includedTypesOnly: new List<Type>() { typeof(IBar) },
	//excludedTypes: new List<Type>() { typeof(IFoo) }
	);
JsonSerializerSettings settings = new JsonSerializerSettings()
{
	Formatting = Formatting.Indented
};
settings.Converters.Add(converter);

//complex dynamic structure to serialize
var list = new List<IBar>()
{
	new C(),
	new D(),
	new E() 
	{ 
		bars = new IBar[2] { 
			new C() { a = 99 },
			new D() { b = "dynamic data", IgnoredString = "IGNORED DATA"} 
		},
		foos = new List<IFoo>() { 
			new A() {a = "123456789"},
			new B() { b = 999999 } 
		} 
	}
};

string polymorphJsonString = JsonConvert.SerializeObject(list, settings);
Console.WriteLine(polymorphJsonString);
var result = JsonConvert.DeserializeObject<IBar[]>(polymorphJsonString, settings);
Console.WriteLine();