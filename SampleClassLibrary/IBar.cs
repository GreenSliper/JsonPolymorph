
using JsonPolymorph;
using Newtonsoft.Json;

namespace SampleClassLibrary
{
	public interface IBar
	{

	}

	public interface IFoo
	{
	}

	public class A : IFoo
	{
		public string a;
	}

	public class B : IFoo
	{
		public int b;
	}

	public class C : IBar
	{
		public int a = 12;
	}

	public class D : IBar
	{
		public string b = "sample data";
		[JsonConverter(typeof(JsonSingleTypeConverter<IFoo>))]
		public IFoo Foo { get; set; }
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
}