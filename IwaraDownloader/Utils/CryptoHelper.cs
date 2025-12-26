using System.Security.Cryptography;
using System.Text;

namespace IwaraDownloader.Utils
{
    /// <summary>
    /// 暗号化ヘルパー
    /// パスワードなどの機密情報を暗号化/復号化する
    /// </summary>
    public static class CryptoHelper
    {
        // マシン固有のエントロピーを使用（同じPCでのみ復号可能）
        private static readonly byte[] AdditionalEntropy = Encoding.UTF8.GetBytes("IwaraDownloader_v1");

        /// <summary>
        /// 文字列を暗号化
        /// </summary>
        /// <param name="plainText">平文</param>
        /// <returns>暗号化された文字列（Base64）</returns>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"暗号化エラー: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 暗号化された文字列を復号化
        /// </summary>
        /// <param name="encryptedText">暗号化された文字列（Base64）</param>
        /// <returns>復号化された平文</returns>
        public static string Decrypt(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] plainBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    AdditionalEntropy,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"復号化エラー: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
