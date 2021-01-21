using System.ComponentModel;

namespace DustyBot.Core.Miscellaneous
{
    public struct NullObject<T>
    {
        [DefaultValue(true)]
        private bool _isNull; // default property initializers are not supported for structs

        public NullObject(T item) : this(item, item == null)
        {
        }

        private NullObject(T item, bool isNull) : this()
        {
            _isNull = isNull;
            Item = item;
        }

        public static NullObject<T> Null()
        {
            return default;
        }

        public T Item { get; }

        public bool IsNull()
        {
            return _isNull;
        }

        public static implicit operator T(NullObject<T> nullObject)
        {
            return nullObject.Item;
        }

        public static implicit operator NullObject<T>(T item)
        {
            return new NullObject<T>(item);
        }

        public override string ToString()
        {
            return (Item != null) ? Item.ToString() : "NULL";
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return IsNull();

            if (!(obj is NullObject<T>))
                return false;

            var no = (NullObject<T>)obj;

            if (IsNull())
                return no.IsNull();

            if (no.IsNull())
                return false;

            return Item.Equals(no.Item);
        }

        public override int GetHashCode()
        {
            if (_isNull)
                return 0;

            var result = Item.GetHashCode();

            if (result >= 0)
                result++;

            return result;
        }
    }
}
