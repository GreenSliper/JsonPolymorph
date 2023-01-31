
using JsonPolymorph;
using Newtonsoft.Json;
using SampleClassLibrary;
using Xunit.Abstractions;

namespace JsonPolymorphTests
{
	public class SpeedTest
	{

		private readonly ITestOutputHelper output;

		public SpeedTest(ITestOutputHelper output)
		{
			this.output = output;
		}

		List<IBar> list = new List<IBar>()
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

		[Theory]
		[InlineData(100)]
		[InlineData(1000)]
		[InlineData(10000)]
		public void ComplexTest(int count)
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
				polymorphResult = JsonConvert.SerializeObject(list, settings);
			}
			watch.Stop();
			output.WriteLine($"JsonPolymorph {count} serialize execution time: {watch.ElapsedMilliseconds} ms");

			watch.Restart();
			for (int i = 0; i < count; i++)
			{
				standardResult = JsonConvert.SerializeObject(list, settingsDefault);
			}
			watch.Stop();
			output.WriteLine($"Default {count} serialize execution time: {watch.ElapsedMilliseconds} ms");

			watch.Restart();
			for (int i = 0; i < count; i++)
			{
				JsonConvert.DeserializeObject<IBar[]>(polymorphResult, settings);
			}
			watch.Stop();
			output.WriteLine($"JsonPolymorph {count} DEserialize execution time: {watch.ElapsedMilliseconds} ms");

			watch.Restart();
			for (int i = 0; i < count; i++)
			{
				JsonConvert.DeserializeObject<IBar[]>(standardResult, settingsDefault);
			}
			watch.Stop();
			output.WriteLine($"Default {count} DEserialize execution time: {watch.ElapsedMilliseconds} ms");
		}
	}
}