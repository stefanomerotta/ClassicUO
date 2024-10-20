using System;

namespace ClassicUO.Network.Encryptions.Login
{
    internal sealed class LoginEncryption1_25_36 : LoginEncryption
    {
        public LoginEncryption1_25_36(uint seed, uint k1, uint k2, uint k3)
            : base(seed, k1, k2, k3)
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

                _key[1] = (_k1 >> (byte)(5 * table1 * table1 & 0xFF)) + table1 * _k1 + table0 * table0 * 0x35ce9581 + 0x07afcc37;
                _key[0] = (_k2 >> (byte)(3 * table0 * table0 & 0xFF)) + table0 * _k2 + _key[1] * _key[1] * 0x4c3a1353 + 0x16ef783f;
            }
        }
    }
}
