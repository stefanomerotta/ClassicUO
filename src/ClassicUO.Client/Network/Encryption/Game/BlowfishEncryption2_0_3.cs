using System;

namespace ClassicUO.Network.Encryption.Game
{
    internal sealed class BlowfishEncryption2_0_3 : BlowfishEncryption
    {
        private readonly TwofishEncryption _twoFishEncryption;

        public BlowfishEncryption2_0_3(uint seed)
        {
            _twoFishEncryption = new(seed, false);
        }

        public override void Encrypt(Span<byte> span)
        {
            base.Encrypt(span);
            _twoFishEncryption.Encrypt(span);
        }

        public override void Decrypt(Span<byte> span)
        { }
    }
}
