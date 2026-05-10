using System;
using System.Security.Cryptography;

namespace PdfUtility.Core.Internal
{
    /// <summary>
    /// PDF 標準セキュリティハンドラー（V=1/2, R=2/3）の最小実装。
    /// 「空のオープンパスワードで開けるか」の判定にだけ使用する。
    /// </summary>
    internal static class PdfStandardSecurity
    {
        /// <summary>PDF 仕様で定められたパスワードのパディング文字列（32 バイト）。</summary>
        private static readonly byte[] Padding =
        {
            0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41,
            0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
            0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80,
            0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A
        };

        /// <summary>
        /// 空のユーザーパスワードで /U と一致するかを検証する。
        /// 一致 → オープンパスワード不要（パーミッションのみ）。
        /// </summary>
        internal static bool IsEmptyUserPassword(
            byte[] o, int p, byte[] firstId, byte[] storedU,
            int v, int r, int keyLengthBytes)
        {
            if (o == null || firstId == null || storedU == null) return false;
            if (o.Length < 32 || storedU.Length < 32) return false;
            if (keyLengthBytes <= 0 || keyLengthBytes > 16) keyLengthBytes = 5; // V=1 既定

            byte[] computedU = ComputeUForEmptyPassword(o, p, firstId, v, r, keyLengthBytes);
            // R=2 → 32 バイト全体を比較。R>=3 → 先頭 16 バイトのみ意味がある。
            int compareLen = r == 2 ? 32 : 16;
            for (int i = 0; i < compareLen; i++)
                if (storedU[i] != computedU[i]) return false;
            return true;
        }

        /// <summary>
        /// 空パスワードを前提とした /U の理論値を計算する。
        /// </summary>
        private static byte[] ComputeUForEmptyPassword(
            byte[] o, int p, byte[] firstId, int v, int r, int keyLengthBytes)
        {
            byte[] key = ComputeEncryptionKey(Padding, o, p, firstId, r, keyLengthBytes);

            if (r == 2)
            {
                // U = RC4(key, padding)
                return Rc4(key, Padding);
            }

            // R >= 3: U = RC4(key XOR i, ... RC4(key, MD5(padding + firstId)) ...) を 0..19 で適用
            byte[] hashInput = new byte[Padding.Length + firstId.Length];
            Buffer.BlockCopy(Padding, 0, hashInput, 0, Padding.Length);
            Buffer.BlockCopy(firstId, 0, hashInput, Padding.Length, firstId.Length);

            byte[] hash;
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(hashInput);

            byte[] result = Rc4(key, hash);
            byte[] xorKey = new byte[key.Length];
            for (int i = 1; i <= 19; i++)
            {
                for (int j = 0; j < key.Length; j++)
                    xorKey[j] = (byte)(key[j] ^ i);
                result = Rc4(xorKey, result);
            }

            // /U は 32 バイトだが先頭 16 バイトのみが検証対象。残りは任意のパディングで構わない。
            byte[] u32 = new byte[32];
            Buffer.BlockCopy(result, 0, u32, 0, Math.Min(16, result.Length));
            return u32;
        }

        /// <summary>
        /// 標準セキュリティハンドラーの暗号化鍵を計算する（パスワード→鍵）。
        /// </summary>
        internal static byte[] ComputeEncryptionKey(
            byte[] paddedPwd, byte[] o, int p, byte[] firstId, int r, int keyLengthBytes)
        {
            byte[] pBytes =
            {
                (byte)(p & 0xFF),
                (byte)((p >> 8) & 0xFF),
                (byte)((p >> 16) & 0xFF),
                (byte)((p >> 24) & 0xFF)
            };

            int totalLen = paddedPwd.Length + o.Length + 4 + firstId.Length;
            byte[] input = new byte[totalLen];
            int pos = 0;
            Buffer.BlockCopy(paddedPwd, 0, input, pos, paddedPwd.Length); pos += paddedPwd.Length;
            Buffer.BlockCopy(o, 0, input, pos, o.Length); pos += o.Length;
            Buffer.BlockCopy(pBytes, 0, input, pos, 4); pos += 4;
            Buffer.BlockCopy(firstId, 0, input, pos, firstId.Length);

            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(input);
                if (r >= 3)
                {
                    byte[] partial = new byte[keyLengthBytes];
                    for (int i = 0; i < 50; i++)
                    {
                        Buffer.BlockCopy(hash, 0, partial, 0, keyLengthBytes);
                        hash = md5.ComputeHash(partial);
                    }
                }
                byte[] key = new byte[keyLengthBytes];
                Buffer.BlockCopy(hash, 0, key, 0, keyLengthBytes);
                return key;
            }
        }

        /// <summary>
        /// 与えられたパスワードを 32 バイトにパディングする（PDF 仕様 7.6.3.3）。
        /// </summary>
        internal static byte[] PadPassword(byte[] password)
        {
            byte[] result = new byte[32];
            int len = Math.Min(password?.Length ?? 0, 32);
            if (len > 0) Buffer.BlockCopy(password, 0, result, 0, len);
            for (int i = len; i < 32; i++) result[i] = Padding[i - len];
            return result;
        }

        /// <summary>
        /// /O の理論値を計算する（オーナーパスワード → 鍵 → ユーザーパディングを RC4）。
        /// </summary>
        internal static byte[] ComputeO(byte[] ownerPwd, byte[] userPwd, int r, int keyLengthBytes)
        {
            byte[] paddedOwner = PadPassword(ownerPwd != null && ownerPwd.Length > 0 ? ownerPwd : userPwd);
            byte[] hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(paddedOwner);
                if (r >= 3)
                {
                    byte[] partial = new byte[keyLengthBytes];
                    for (int i = 0; i < 50; i++)
                    {
                        Buffer.BlockCopy(hash, 0, partial, 0, keyLengthBytes);
                        hash = md5.ComputeHash(partial);
                    }
                }
            }
            byte[] key = new byte[keyLengthBytes];
            Buffer.BlockCopy(hash, 0, key, 0, keyLengthBytes);

            byte[] paddedUser = PadPassword(userPwd);
            byte[] result = Rc4(key, paddedUser);
            if (r >= 3)
            {
                byte[] xorKey = new byte[keyLengthBytes];
                for (int i = 1; i <= 19; i++)
                {
                    for (int j = 0; j < keyLengthBytes; j++)
                        xorKey[j] = (byte)(key[j] ^ i);
                    result = Rc4(xorKey, result);
                }
            }
            return result;
        }

        /// <summary>
        /// /U の理論値を計算する（パスワード → 鍵 → 規定値を RC4）。
        /// </summary>
        internal static byte[] ComputeU(
            byte[] userPwd, byte[] o, int p, byte[] firstId, int r, int keyLengthBytes)
        {
            byte[] padded = PadPassword(userPwd);
            byte[] key = ComputeEncryptionKey(padded, o, p, firstId, r, keyLengthBytes);
            if (r == 2)
                return Rc4(key, Padding);

            byte[] hashInput = new byte[Padding.Length + firstId.Length];
            Buffer.BlockCopy(Padding, 0, hashInput, 0, Padding.Length);
            Buffer.BlockCopy(firstId, 0, hashInput, Padding.Length, firstId.Length);

            byte[] hash;
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(hashInput);

            byte[] result = Rc4(key, hash);
            byte[] xorKey = new byte[key.Length];
            for (int i = 1; i <= 19; i++)
            {
                for (int j = 0; j < key.Length; j++)
                    xorKey[j] = (byte)(key[j] ^ i);
                result = Rc4(xorKey, result);
            }
            byte[] u32 = new byte[32];
            Buffer.BlockCopy(result, 0, u32, 0, Math.Min(16, result.Length));
            return u32;
        }

        /// <summary>
        /// RC4 ストリーム暗号。/U・/O 計算で使用。
        /// </summary>
        internal static byte[] Rc4(byte[] key, byte[] data)
        {
            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++) s[i] = (byte)i;
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 0xFF;
                byte tmp = s[i]; s[i] = s[j]; s[j] = tmp;
            }

            byte[] output = new byte[data.Length];
            int ii = 0, jj = 0;
            for (int k = 0; k < data.Length; k++)
            {
                ii = (ii + 1) & 0xFF;
                jj = (jj + s[ii]) & 0xFF;
                byte tmp = s[ii]; s[ii] = s[jj]; s[jj] = tmp;
                output[k] = (byte)(data[k] ^ s[(s[ii] + s[jj]) & 0xFF]);
            }
            return output;
        }
    }
}
