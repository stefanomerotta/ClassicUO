using System;

namespace ClassicUO.Network.Encryption.Login
{
    internal sealed class LoginEncryptionOld : LoginEncryption
    {
        public LoginEncryptionOld(uint seed, uint k1, uint k2)
            : base(seed, k1, k2, 0)
        { }

        public override void Encrypt(Span<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = (byte)(span[i] ^ (byte)_key[0]);

                uint table0 = _key[0];
                uint table1 = _key[1];

                _key[0] = (table0 >> 1 | table1 << 31) ^ _k2;
                _key[1] = (table1 >> 1 | table0 << 31) ^ _k1;
            }
        }
    }
}
