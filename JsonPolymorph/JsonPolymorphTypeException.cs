using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonPolymorph
{
	public class JsonPolymorphTypeException : Exception
	{
		public JsonPolymorphTypeException() : base("JsonPolymporph (de)serialization error!") 
		{
		}
		public JsonPolymorphTypeException(string message) : base(message)
		{
		}
		public JsonPolymorphTypeException(string message, Exception? innerException) : base(message, innerException)
		{
		}
	}
}
