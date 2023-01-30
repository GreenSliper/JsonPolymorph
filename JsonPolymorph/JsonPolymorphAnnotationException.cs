using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonPolymorph
{
	public class JsonPolymorphAnnotationException : Exception
	{
		public JsonPolymorphAnnotationException() : base("JsonPolymporph annotation not found! Cannot resolve types!")
		{
		}
		public JsonPolymorphAnnotationException(string message) : base(message)
		{
		}
		public JsonPolymorphAnnotationException(string message, Exception? innerException) : base(message, innerException)
		{
		}
	}
}
