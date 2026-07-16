using System;
using System.Collections.Generic;
using System.Text;

namespace IPCamTester
{
    public enum ErrorType
    {
        Ping,
        Capture,
    }

    public class Error
    {

        private ErrorType _type;
        private String _msg;

        public Error(ErrorType type, String msg)
        {
            this._type = type;
            this._msg = msg;
        }

        public ErrorType GetErrorType()
        {
            return _type;
        }

        public override string ToString()
        {
            return $"{this._type} error: {this._msg}";
        }
    }

    public class Result
    {
        public bool CanPing { get; set; }
        public bool CanCapture { get; set; }
    }
}
