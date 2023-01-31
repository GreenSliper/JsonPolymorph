using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JsonPolymorph
{
	internal static class JsonPolymorphExtensions
	{
		public static Func<object?[], object> GetConstructor(this Type type)
		{
			return type.GetConstructor(flags, Type.EmptyTypes).Invoke;
		}

		public static IEnumerable<Type> GetParentTypes(this Type type)
		{
			// is there any base type?
			if (type == null)
			{
				yield break;
			}
			yield return type;
			// return all implemented or inherited interfaces
			foreach (var i in type.GetInterfaces())
			{
				yield return i;
			}

			// return all inherited types
			var currentBaseType = type.BaseType;
			while (currentBaseType != null)
			{
				yield return currentBaseType;
				currentBaseType = currentBaseType.BaseType;
			}
		}

		private static readonly HashSet<Type> ValTupleTypes = new HashSet<Type>(
			new Type[] { typeof(ValueTuple<>), typeof(ValueTuple<,>),
						 typeof(ValueTuple<,,>), typeof(ValueTuple<,,,>),
						 typeof(ValueTuple<,,,,>), typeof(ValueTuple<,,,,,>),
						 typeof(ValueTuple<,,,,,,>), typeof(ValueTuple<,,,,,,,>)
			}
		);
		public static bool IsValueTuple(this Type t)
		{
			return t.IsGenericType
				&& ValTupleTypes.Contains(t.GetGenericTypeDefinition());
		}

		static readonly IDictionary<Type, Func<object, object[]>> GetItems = new Dictionary<Type, Func<object, object[]>>
		{
			[typeof(ValueTuple<>)] = o => new object[] { ((dynamic)o).Item1 },
			[typeof(ValueTuple<,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2 },
			[typeof(ValueTuple<,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3 },
			[typeof(ValueTuple<,,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3, ((dynamic)o).Item4 },
			[typeof(ValueTuple<,,,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3, ((dynamic)o).Item4, ((dynamic)o).Item5 },
			[typeof(ValueTuple<,,,,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3, ((dynamic)o).Item4, ((dynamic)o).Item5, ((dynamic)o).Item6 },
			[typeof(ValueTuple<,,,,,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3, ((dynamic)o).Item4, ((dynamic)o).Item5, ((dynamic)o).Item6, ((dynamic)o).Item7 },
			[typeof(ValueTuple<,,,,,,,>)] = o => new object[] { ((dynamic)o).Item1, ((dynamic)o).Item2, ((dynamic)o).Item3, ((dynamic)o).Item4, ((dynamic)o).Item5, ((dynamic)o).Item6, ((dynamic)o).Item7, ((dynamic)o).Rest }
		};

		public static object[] GetTupleValues(this Type tp, object obj)
		{
			object[] items = null!;
			if (tp.IsGenericType && GetItems.TryGetValue(tp.GetGenericTypeDefinition(), out var itemGetter))
				items = itemGetter(obj);
			return items;
		}
		public static Type[] GetTupleTypes(this Type tp)
		{
			return tp.GetGenericArguments();
		}

		public static bool IsKeyValuePair(this Type t)
		{
			return t!=null && t.IsGenericType
				&& typeof(KeyValuePair<,>) == t.GetGenericTypeDefinition();
		}

		static BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;

		public static (Type, Type) GetKeyValuePairTypes(this Type tp)
		{
			(Type, Type) types = default;
			if (tp.IsGenericType && typeof(KeyValuePair<,>) == tp.GetGenericTypeDefinition())
				types = (tp.GetProperty("Key", flags)?.PropertyType!, tp.GetProperty("Value", flags)?.PropertyType!);
			return types;
		}

		public static (object, object) GetKeyValuePairValues(this Type tp, object obj)
		{
			(object, object) items = default;
			if (tp.IsGenericType && typeof(KeyValuePair<,>) == tp.GetGenericTypeDefinition())
				items =  (((dynamic)obj).Key, ((dynamic)obj).Value);
			return items;
		}

		static HashSet<Type> JValueTypes = new HashSet<Type>()
		{
			typeof(DateTime),
			typeof(DateTimeOffset),
			typeof(TimeSpan),
			typeof(Decimal),
			typeof(String),
			typeof(Guid),
			typeof(Uri),
		};
		public static bool IsJValueType(this Type type)
		{
			return type.IsPrimitive || JValueTypes.Contains(type);
		}
	}
}
