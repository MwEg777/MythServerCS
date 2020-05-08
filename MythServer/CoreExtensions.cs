using System.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CoreExtensions
{ 
public static class CoreExtensions
{

    public static string ToJson(this Dictionary<string, string> dict)
    {

        return JsonConvert.SerializeObject(dict) + "$eof$";

    }
    public static T GetDefault<T>(this Dictionary<string, T> instance, string key, T val = default(T))
    {
        if (instance.ContainsKey(key))
            return instance[key];
        return val;
    }


    public static void SetDefault<T>(this Dictionary<string, T> instance, string key, T val)
    {

        if (!instance.ContainsKey(key))
            instance.Add(key, val);
        instance[key] = val;
    }
    public static bool Contains<T>(this T[] arr, T item)
    {
        for (int i = 0; i < arr.Length; i++)
            if (arr[i].Equals(item))
                return true;
        return false;
    }

    public static byte[] TakeOnly(this byte[] instance, int length)
    {
        byte[] destfoo = new byte[length];
        Array.Copy(instance, 0, destfoo, 0, length);
        return destfoo;
    }

    public static int[] toIntArray(this string str)
    {
        string[] strArray = str.Split(',');
        int[] intArr = new int[strArray.Length];
        for (int i = 0; i < strArray.Length; i++)
            intArr[i] = int.Parse(strArray[i]);
        return intArr;
    }

    public static string[] toStrArray(this string str)
    {
        string[] strArray = str.Split(',');
        for (int i = 0; i < strArray.Length; i++)
            strArray[i] = strArray[i].Replace("\"", "");
        return strArray;
    }

    public static T GetRandomListElement<T>(this List<T> list)
    {

        return list[new Random().Next(0, list.Count)];

    }

    public static string ToKMB(this int num)
    {
        if (num > 999999999 || num < -999999999) return num.ToString("0,,,.###B", CultureInfo.InvariantCulture);
        if (num > 999999 || num < -999999) return num.ToString("0,,.##M", CultureInfo.InvariantCulture);
        if (num > 999 || num < -999) return num.ToString("0,.#K", CultureInfo.InvariantCulture);
        return num.ToString(CultureInfo.InvariantCulture);
    }

    public static string FirstLetterToUpperCase(this string s)
    {
        if (string.IsNullOrEmpty(s))
            throw new ArgumentException("There is no first letter");

        char[] a = s.ToCharArray();
        a[0] = char.ToUpper(a[0]);
        return new string(a);
    }

    public static string RandomString(int size, bool lowerCase)
    {
        StringBuilder builder = new StringBuilder();
        System.Random random = new System.Random();
        char ch;
        for (int i = 0; i < size; i++)
        {
            ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
            builder.Append(ch);
        }
        if (lowerCase)
            return builder.ToString().ToLower();
        return builder.ToString();
    }

    public static string ConvertToEasternArabicNumerals(this string input)
    {
        System.Text.UTF8Encoding utf8Encoder = new UTF8Encoding();
        System.Text.Decoder utf8Decoder = utf8Encoder.GetDecoder();
        System.Text.StringBuilder convertedChars = new System.Text.StringBuilder();
        char[] convertedChar = new char[1];
        byte[] bytes = new byte[] { 217, 160 };
        char[] inputCharArray = input.ToCharArray();
        foreach (char c in inputCharArray)
        {
            if (char.IsDigit(c))
            {
                bytes[1] = Convert.ToByte(160 + char.GetNumericValue(c));
                utf8Decoder.GetChars(bytes, 0, 2, convertedChar, 0);
                convertedChars.Append(convertedChar[0]);
            }
            else
            {
                convertedChars.Append(c);
            }
        }
        return convertedChars.ToString();
    }

    public static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key)
    {
        byte[] encrypted;
        byte[] IV;

        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Key;

            aesAlg.GenerateIV();
            IV = aesAlg.IV;

            aesAlg.Mode = CipherMode.CBC;

            var encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            // Create the streams used for encryption. 
            using (var msEncrypt = new MemoryStream())
            {
                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (var swEncrypt = new StreamWriter(csEncrypt))
                    {
                        //Write all data to the stream.
                        swEncrypt.Write(plainText);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }
        }

        var combinedIvCt = new byte[IV.Length + encrypted.Length];
        Array.Copy(IV, 0, combinedIvCt, 0, IV.Length);
        Array.Copy(encrypted, 0, combinedIvCt, IV.Length, encrypted.Length);

        // Return the encrypted bytes from the memory stream. 
        return combinedIvCt;

    }

    public static string DecryptStringFromBytes_Aes(byte[] cipherTextCombined, byte[] Key)
    {

        // Declare the string used to hold 
        // the decrypted text. 
        string plaintext = null;

        // Create an Aes object 
        // with the specified key and IV. 
        using (Aes aesAlg = Aes.Create())
        {
            aesAlg.Key = Key;

            byte[] IV = new byte[aesAlg.BlockSize / 8];
            byte[] cipherText = new byte[cipherTextCombined.Length - IV.Length];

            Array.Copy(cipherTextCombined, IV, IV.Length);
            Array.Copy(cipherTextCombined, IV.Length, cipherText, 0, cipherText.Length);

            aesAlg.IV = IV;

            aesAlg.Mode = CipherMode.CBC;

            // Create a decrytor to perform the stream transform.
            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            // Create the streams used for decryption. 
            using (var msDecrypt = new MemoryStream(cipherText))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {

                        // Read the decrypted bytes from the decrypting stream
                        // and place them in a string.
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }

        }

        return plaintext;

    }

    public static float AllHours(this TimeSpan ts)
    {

        float allHours = 0;

        allHours += ts.Seconds / 3600f;

        allHours += ts.Minutes / 60f;

        allHours += ts.Hours;

        allHours += ts.Days * 24f;

        return allHours;

    }

    public static string RemoveWhitespace(this string input)
    {
        return new string(input.ToCharArray()
            .Where(c => !Char.IsWhiteSpace(c))
            .ToArray());
    }

}
}
