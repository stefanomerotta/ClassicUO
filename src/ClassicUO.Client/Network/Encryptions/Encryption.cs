using ClassicUO.Network.Encryptions.Game;
using ClassicUO.Network.Encryptions.Login;
using ClassicUO.Utility;
using System;

namespace ClassicUO.Network.Encryptions
{
    internal abstract class Encryption
    {
        public abstract void Encrypt(Span<byte> span);
        public abstract void Decrypt(Span<byte> span);

        public static Encryption CreateForLogin(ClientVersion version, uint seed)
        {
            if (version == ClientVersion.CV_200X)
                return new LoginEncryption(seed, 0x2D13A5FC, 0x2D13A5FD, 0xA39D527F);

            int a = ((int)version >> 24) & 0xFF;
            int b = ((int)version >> 16) & 0xFF;
            int c = ((int)version >> 8) & 0xFF;

            int temp = ((((a << 9) | b) << 10) | c) ^ ((c * c) << 5);

            uint key2 = (uint)((temp << 4) ^ (b * b) ^ (b * 0x0B000000) ^ (c * 0x380000) ^ 0x2C13A5FD);
            temp = (((((a << 9) | c) << 10) | b) * 8) ^ (c * c * 0x0c00);
            uint key3 = (uint)(temp ^ (b * b) ^ (b * 0x6800000) ^ (c * 0x1c0000) ^ 0x0A31D527F);
            uint key1 = key2 - 1;

            return version switch
            {
                < (ClientVersion)((1 & 0xFF) << 24 | (25 & 0xFF) << 16 | (35 & 0xFF) << 8 | 0 & 0xFF)
                    => new LoginEncryptionOld(seed, key1, key2),

                (ClientVersion)((1 & 0xFF) << 24 | (25 & 0xFF) << 16 | (36 & 0xFF) << 8 | 0 & 0xFF)
                    => new LoginEncryption1_25_36(seed, key1, key2, key3),

                _ => new LoginEncryption(seed, key1, key2, key3),
            };
        }

        public static Encryption CreateForGame(EncryptionType type, uint seed)
        {
            return type switch
            {
                < EncryptionType.BLOWFISH_2_0_3 => new BlowfishEncryption(),
                EncryptionType.BLOWFISH_2_0_3 => new BlowfishEncryption2_0_3(seed),
                _ => new TwofishEncryption(seed, true)
            };
        }

        public static EncryptionType GetType(ClientVersion version)
        {
            if (version == ClientVersion.CV_200X)
                return EncryptionType.BLOWFISH_2_0_3;

            return version switch
            {
                < (ClientVersion)((1 & 0xFF) << 24 | (25 & 0xFF) << 16 | (35 & 0xFF) << 8 | 0 & 0xFF)
                    => EncryptionType.OLD_BFISH,

                (ClientVersion)((1 & 0xFF) << 24 | (25 & 0xFF) << 16 | (36 & 0xFF) << 8 | 0 & 0xFF)
                    => EncryptionType.BLOWFISH_1_25_36,

                <= ClientVersion.CV_200
                    => EncryptionType.BLOWFISH,

                <= (ClientVersion)((2 & 0xFF) << 24 | (0 & 0xFF) << 16 | (3 & 0xFF) << 8 | 0 & 0xFF)
                    => EncryptionType.BLOWFISH_2_0_3,

                _ => EncryptionType.TWOFISH_MD5
            };
        }
    }
}
