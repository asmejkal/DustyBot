using System;

namespace DustyBot.Service.Services.DaumCafe
{
    public class DaumCafeCredentialInfo
    {
        public Guid Id { get; }
        public string Name { get; }

        public DaumCafeCredentialInfo(Guid id, string name)
        {
            Id = id;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}
