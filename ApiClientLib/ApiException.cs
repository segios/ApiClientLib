using System;

namespace ApiClientLib
{
    public class ApiException : Exception
    {
        public ApiException() { }
        public ApiException(string msg) : base(msg) { }
        public ApiException(string msg, Exception ex) : base(msg, ex) { }
    }
}
