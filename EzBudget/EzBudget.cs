using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public sealed class EzBudget
{
    private sealed class CryptoHeader
    {
        public byte[] salt = new byte[32];
        public byte[] iv = new byte[16];
        public CryptoHeader(byte[] salt, byte[] iv)
        {
            this.salt = salt;
            this.iv = iv;
        }
        public CryptoHeader Copy()
        {
            byte[] saltCopy = new byte[salt.Length];
            Array.Copy(salt, saltCopy, salt.Length);
            byte[] ivCopy = new byte[iv.Length];
            Array.Copy(iv, ivCopy, iv.Length);
            return new CryptoHeader(saltCopy, ivCopy);
        }
    }
    private sealed class FileSchema
    {
        public CryptoHeader cryptoHeader = null;
        public byte[] data = new byte[0];
        public FileSchema(CryptoHeader cryptoHeader, byte[] data)
        {
            this.cryptoHeader = cryptoHeader;
            this.data = data;
        }
        public FileSchema Copy()
        {
            CryptoHeader cryptoHeaderCopy = cryptoHeader.Copy();
            byte[] dataCopy = new byte[data.Length];
            Array.Copy(data, dataCopy, data.Length);
            return new FileSchema(cryptoHeaderCopy, dataCopy);
        }
    }
    private sealed class Transaction
    {
        public double amount = 0.0;
        public string description = "";
        public Transaction(double amount, string description)
        {
            this.amount = amount;
            this.description = description;
        }
        public Transaction Copy()
        {
            return new Transaction(amount, description);
        }
    }
    private sealed class BudgetState
    {
        public List<Transaction> transactions = new List<Transaction>();
        public BudgetState(List<Transaction> transactions)
        {
            this.transactions = transactions;
        }
        public BudgetState Copy()
        {
            List<Transaction> transactionsCopy = new List<Transaction>(transactions.Count);
            for (int i = 0; i < transactions.Count; i++)
            {
                transactionsCopy.Add(transactions[i].Copy());
            }
            return new BudgetState(transactionsCopy);
        }
    }

    private string filePath = null;
    private string password = null;
    private bool hasChanges = false;
    private CryptoHeader cryptoHeader = null;
    private int currentStateIndex = 0;
    private List<BudgetState> budgetStates = null;

    private static byte[] StringToBytes(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }
    private static string BytesToString(byte[] value)
    {
        return Encoding.UTF8.GetString(value);
    }
    private static byte[] KeyGen(byte[] password, byte[] salt)
    {
        byte[] passwordWithSalt = new byte[password.Length + salt.Length];
        Array.Copy(password, 0, passwordWithSalt, 0, password.Length);
        Array.Copy(salt, 0, passwordWithSalt, password.Length, salt.Length);
        SHA256 sha256 = SHA256.Create();
        byte[] key = sha256.ComputeHash(passwordWithSalt);
        sha256.Dispose();
        return key;
    }
    private static byte[] Encrypt(byte[] data, byte[] iv, byte[] key)
    {
        Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        MemoryStream outputStream = new MemoryStream();
        CryptoStream cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

        cryptoStream.Write(data, 0, data.Length);
        cryptoStream.FlushFinalBlock();

        byte[] output = outputStream.ToArray();

        cryptoStream.Dispose();
        outputStream.Dispose();
        encryptor.Dispose();
        aes.Dispose();

        return output;
    }
    private static byte[] Decrypt(byte[] data, byte[] iv, byte[] key)
    {
        Aes aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        MemoryStream inputStream = new MemoryStream(data);
        CryptoStream cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
        MemoryStream outputStream = new MemoryStream();

        try
        {
            cryptoStream.CopyTo(outputStream);
        }
        catch
        {
            throw new Exception("Bad key");
        }

        byte[] output = outputStream.ToArray();

        outputStream.Dispose();
        cryptoStream.Dispose();
        inputStream.Dispose();
        aes.Dispose();

        return output;
    }

    public void Save()
    {
        string json = JsonConvert.SerializeObject(budgetStates[currentStateIndex]);
        byte[] jsonBytes = StringToBytes(json);
        byte[] key = KeyGen(StringToBytes(password), cryptoHeader.salt);
        byte[] encryptedJson = Encrypt(jsonBytes, cryptoHeader.iv, key);
        FileSchema fileSchema = new FileSchema(cryptoHeader, encryptedJson);
        string json2 = JsonConvert.SerializeObject(fileSchema);
        File.WriteAllText(filePath, json2);
        hasChanges = false;
    }
    public static EzBudget Load(string filePath, string password)
    {
        EzBudget output = new EzBudget();
        output.filePath = filePath;
        output.password = password;
        string json = File.ReadAllText(filePath);
        FileSchema fileSchema = JsonConvert.DeserializeObject<FileSchema>(json);
        output.cryptoHeader = fileSchema.cryptoHeader;
        output.currentStateIndex = 0;
        output.budgetStates = new List<BudgetState>();
        byte[] passwordBytes = StringToBytes(password);
        byte[] key = KeyGen(passwordBytes, output.cryptoHeader.salt);
        byte[] json2Bytes;
        try
        {
            json2Bytes = Decrypt(fileSchema.data, output.cryptoHeader.iv, key);
        }
        catch
        {
            return null;
        }
        string json2 = BytesToString(json2Bytes);
        output.budgetStates.Add(JsonConvert.DeserializeObject<BudgetState>(json2));
        return output;
    }
    public static EzBudget New(string filePath, string password)
    {
        EzBudget output = new EzBudget();
        output.filePath = filePath;
        output.password = password;
        byte[] salt = new byte[32];
        byte[] iv = new byte[16];
        RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        rng.GetBytes(iv);
        rng.Dispose();
        output.cryptoHeader = new CryptoHeader(salt, iv);
        output.currentStateIndex = 0;
        output.budgetStates = new List<BudgetState>();
        output.budgetStates.Add(new BudgetState(new List<Transaction>()));
        output.Save();
        return output;
    }
    public static EzBudget NewFromChaseExport(string filePath, string password, string chaseExportPath)
    {
        EzBudget output = new EzBudget();
        output.filePath = filePath;
        output.password = password;
        byte[] salt = new byte[32];
        byte[] iv = new byte[16];
        RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);
        rng.GetBytes(iv);
        rng.Dispose();
        output.cryptoHeader = new CryptoHeader(salt, iv);
        output.currentStateIndex = 0;
        output.budgetStates = new List<BudgetState>();
        ParsedChaseImport parsedChaseImport = null;
        try
        {
            parsedChaseImport = MicroServiceBClient.ChaseImport(File.ReadAllText(chaseExportPath));
        }
        catch
        {
            return null;
        }
        Transaction[] transactions = new Transaction[parsedChaseImport.Amounts.Length];
        for (int i = 0; i < transactions.Length; i++)
        {
            transactions[i] = new Transaction(parsedChaseImport.Amounts[i], parsedChaseImport.Descriptions[i]);
        }
        output.budgetStates.Add(new BudgetState(new List<Transaction>(transactions)));
        output.Save();
        return output;
    }

    private void SaveState()
    {
        while (currentStateIndex != budgetStates.Count - 1)
        {
            budgetStates.RemoveAt(budgetStates.Count - 1);
        }
        budgetStates.Add(budgetStates[budgetStates.Count - 1].Copy());
        currentStateIndex++;
    }
    public void Undo()
    {
        currentStateIndex--;
        if (currentStateIndex < 0)
        {
            currentStateIndex = 0;
        }
        hasChanges = true;
    }
    public void Redo()
    {
        currentStateIndex++;
        if (currentStateIndex >= budgetStates.Count)
        {
            currentStateIndex = budgetStates.Count - 1;
        }
        hasChanges = true;
    }
    public int Count()
    {
        return budgetStates[currentStateIndex].transactions.Count;
    }
    public void Add(double amount, string description)
    {
        SaveState();
        budgetStates[currentStateIndex].transactions.Add(new Transaction(amount, description));
        hasChanges = true;
    }
    public void Remove(int transactionIndex)
    {
        SaveState();
        budgetStates[currentStateIndex].transactions.RemoveAt(transactionIndex);
        hasChanges = true;
    }
    public double GetAmount(int transactionIndex)
    {
        return budgetStates[currentStateIndex].transactions[transactionIndex].amount;
    }
    public string GetDescription(int transactionIndex)
    {
        return budgetStates[currentStateIndex].transactions[transactionIndex].description;
    }
    public void SetDescription(int transactionIndex, string value)
    {
        SaveState();
        budgetStates[currentStateIndex].transactions[transactionIndex].description = value;
        hasChanges = true;
    }
    public void SetAmount(int transactionIndex, double value)
    {
        SaveState();
        budgetStates[currentStateIndex].transactions[transactionIndex].amount = value;
        hasChanges = true;
    }
    public bool GetHasChanges()
    {
        return hasChanges;
    }
}