
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
		[JsonIgnore]
		public string IgnoredString { get; set; }
	}

	public class E : IBar
	{
		public IBar[] bars;
		public List<IFoo> foos;
	}
}