using System;
using System.Collections.Generic;
using System.Text;

namespace LazyMagic
{
    public class AwsAuthenticationConfig
    {
        public string Name { get; set; }
        public string Template { get; set; }
        public string CallbackURL { get; set; }
        public string LogoutURL{ get; set; }
        public int DeleteAfterDays { get; set; }
        public int StartWindowMinutes { get; set; }
        public string ScheduleExpression { get; set; }
        public int SecurityLevel { get; set; }
    }
}
