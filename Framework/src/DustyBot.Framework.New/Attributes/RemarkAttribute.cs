﻿using System;

namespace DustyBot.Framework.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
    public class RemarkAttribute : Attribute
    {
        public string Remark { get; }

        public RemarkAttribute(string remark)
        {
            Remark = remark;
        }
    }
}
