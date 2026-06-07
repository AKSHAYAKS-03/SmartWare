using System.Threading.Tasks;

namespace SmartInventory.Core.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
