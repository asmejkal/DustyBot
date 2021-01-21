using System;

namespace DustyBot.Framework.Modules.Attributes
{
    [AttributeUsage(AttributeTargets.All, Inherited = true, AllowMultiple = false)]
    public class CommentAttribute : Attribute
    {
        public CommentAttribute(string comment)
        {
            Comment = comment;
        }

        public string Comment { get; }
    }
}
