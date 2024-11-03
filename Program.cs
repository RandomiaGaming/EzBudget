using Newtonsoft.Json;
using ScaleForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Security.Cryptography;
using System.Text;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        EzBudgetForm ezBudget = new EzBudgetForm();
        Application.Run(ezBudget);
    }
}
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

public sealed class EzBudgetForm : Form
{
    private EzBudget loadedBudget = null;
    private ToolTip toolTip = null;
    private int scrollOffset = 0;

    private Scaled<Panel> fileScreen = null;
    private Scaled<Label> fileScreen_header = null;
    private Scaled<Button> fileScreen_newButton = null;
    private Scaled<Button> fileScreen_openButton = null;

    private Scaled<Panel> mainScreen = null;
    private Scaled<Label> mainScreen_header = null;
    private Scaled<Button> mainScreen_saveButton = null;
    private Scaled<Button> mainScreen_undoButton = null;
    private Scaled<Button> mainScreen_redoButton = null;
    private Scaled<Button> mainScreen_newButton = null;
    private sealed class TransactionPanel
    {
        public Scaled<Panel> container;
        public Scaled<TextBox> amountBox;
        public Scaled<TextBox> descriptionBox;
        public Scaled<Button> deleteButton;
    }
    private TransactionPanel[] mainScreen_transactions = null;

    public EzBudgetForm()
    {
        this.KeyPreview = true;
        this.KeyDown += form_keyDown;
        this.Text = "EzBudget 1.0";
        this.StartPosition = FormStartPosition.Manual;
        this.Size = new Size(Screen.PrimaryScreen.Bounds.Width / 2, Screen.PrimaryScreen.Bounds.Height / 2);
        this.Location = new Point(Screen.PrimaryScreen.Bounds.X + (Screen.PrimaryScreen.Bounds.Width / 4), Screen.PrimaryScreen.Bounds.Y + (Screen.PrimaryScreen.Bounds.Height / 4));

        toolTip = new ToolTip();



        fileScreen = new Scaled<Panel>(this, 0, 0, 1, 1);
        fileScreen.Control.BackColor = Color.FromArgb(255, 254, 190, 139);

        fileScreen_header = new Scaled<Label>(fileScreen, 0, 0.9, 1, 0.1);
        fileScreen_header.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        fileScreen_header.Control.Text = "EzBudget 1.0";
        fileScreen_header.Control.TextAlign = ContentAlignment.MiddleCenter;

        fileScreen_newButton = new Scaled<Button>(fileScreen, 0, 0, 0.5, 0.1);
        fileScreen_newButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        fileScreen_newButton.Control.Text = "New Budget";
        fileScreen_newButton.Control.Click += fileScreen_newButton_click;
        toolTip.SetToolTip(fileScreen_newButton.Control, "Creates a new blank budget. You will be asked to choose a password and file location.");

        fileScreen_openButton = new Scaled<Button>(fileScreen, 0.5, 0, 0.5, 0.1);
        fileScreen_openButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        fileScreen_openButton.Control.Text = "Open Budget";
        fileScreen_openButton.Control.Click += fileScreen_openButton_click;
        toolTip.SetToolTip(fileScreen_openButton.Control, "Opens an existing budget. You will be asked so select the file location and input the password.");



        mainScreen = new Scaled<Panel>(this, 0, 0, 1, 1);
        mainScreen.Control.BackColor = Color.FromArgb(255, 254, 190, 139);
        mainScreen.Control.MouseWheel += mainScreen_mouseWheel;

        mainScreen_saveButton = new Scaled<Button>(mainScreen, 0, 0.9, 0.1, 0.1);
        mainScreen_saveButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        mainScreen_saveButton.Control.Text = "Save";
        mainScreen_saveButton.Control.Click += mainScreen_saveButton_click;
        toolTip.SetToolTip(mainScreen_saveButton.Control, "Click to save. Or press CTRL+S.");

        mainScreen_header = new Scaled<Label>(mainScreen, 0.1, 0.9, 0.7, 0.1);
        mainScreen_header.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        mainScreen_header.Control.Text = "EzBudget 1.0";
        mainScreen_header.Control.TextAlign = ContentAlignment.MiddleCenter;

        mainScreen_undoButton = new Scaled<Button>(mainScreen, 0.8, 0.9, 0.1, 0.1);
        mainScreen_undoButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        mainScreen_undoButton.Control.Text = "Undo";
        mainScreen_undoButton.Control.Click += mainScreen_undoButton_click;
        toolTip.SetToolTip(mainScreen_undoButton.Control, "Click to undo. Or press CTRL+Z.");

        mainScreen_redoButton = new Scaled<Button>(mainScreen, 0.9, 0.9, 0.1, 0.1);
        mainScreen_redoButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        mainScreen_redoButton.Control.Text = "Redo";
        mainScreen_redoButton.Control.Click += mainScreen_redoButton_click;
        toolTip.SetToolTip(mainScreen_redoButton.Control, "Click to redo. Or press CTRL+Y.");

        mainScreen_transactions = new TransactionPanel[8];
        for (int i = 0; i < 8; i++)
        {
            mainScreen_transactions[i] = new TransactionPanel();

            mainScreen_transactions[i].container = new Scaled<Panel>(mainScreen, 0, 1.0 - ((i + 2) / 10.0), 1, 0.1);

            mainScreen_transactions[i].amountBox = new Scaled<TextBox>(mainScreen_transactions[i].container, 0, 0, 0.15, 1);
            mainScreen_transactions[i].amountBox.Control.BorderStyle = BorderStyle.None;
            mainScreen_transactions[i].amountBox.Control.BackColor = Color.FromArgb(255, 254, 190, 139);
            mainScreen_transactions[i].amountBox.Control.TextAlign = HorizontalAlignment.Center;
            mainScreen_transactions[i].amountBox.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
            mainScreen_transactions[i].amountBox.Control.Leave += mainScreen_amountBox_leave;
            mainScreen_transactions[i].amountBox.Control.KeyPress += mainScreen_amountBox_keyPress;
            toolTip.SetToolTip(mainScreen_transactions[i].amountBox.Control, "Click to change transaction amount. Input must be a valid number.");

            mainScreen_transactions[i].descriptionBox = new Scaled<TextBox>(mainScreen_transactions[i].container, 0.15, 0, 0.7, 1);
            mainScreen_transactions[i].descriptionBox.Control.BorderStyle = BorderStyle.None;
            mainScreen_transactions[i].descriptionBox.Control.BackColor = Color.FromArgb(255, 254, 190, 139);
            mainScreen_transactions[i].descriptionBox.Control.TextAlign = HorizontalAlignment.Center;
            mainScreen_transactions[i].descriptionBox.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
            mainScreen_transactions[i].descriptionBox.Control.Leave += mainScreen_descriptionBox_leave;
            mainScreen_transactions[i].descriptionBox.Control.KeyPress += mainScreen_descriptionBox_keyPress;
            toolTip.SetToolTip(mainScreen_transactions[i].descriptionBox.Control, "Click to change transaction description.");

            mainScreen_transactions[i].deleteButton = new Scaled<Button>(mainScreen_transactions[i].container, 0.85, 0, 0.15, 1);
            mainScreen_transactions[i].deleteButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
            mainScreen_transactions[i].deleteButton.Control.Text = "Delete";
            mainScreen_transactions[i].deleteButton.Control.Click += mainScreen_deleteButton_click;
            toolTip.SetToolTip(mainScreen_transactions[i].deleteButton.Control, "Click to delete a transaction. Don't worry this can be undone.");
        }

        mainScreen_newButton = new Scaled<Button>(mainScreen, 0, 0, 1, 0.1);
        mainScreen_newButton.Control.Font = new Font(FontFamily.GenericSansSerif, 20.0f, FontStyle.Regular);
        mainScreen_newButton.Control.Text = "New Transaction";
        mainScreen_newButton.Control.Click += mainScreen_newButton_click;
        toolTip.SetToolTip(mainScreen_newButton.Control, "Click to add a new a transaction. You can delete transactions later if needed.");



        RefreshLayout();
    }
    private string AmountToString(double value)
    {
        bool isNegative = value < 0.0;
        if (isNegative)
        {
            value = -value;
            return $"-${value:F2}";
        }
        else
        {
            return $"${value:F2}";
        }
    }
    private double StringToAmount(string value)
    {
        if (value.Length >= 1 && value[0] == '$')
        {
            value = value.Substring(1);
        }
        if (value.Length >= 2 && (value[0] == '-' || value[0] == '+') && value[1] == '$')
        {
            value = value[0] + value.Substring(2);
        }
        double output = 0.0;
        if (!double.TryParse(value, out output))
        {
            return double.NaN;
        }
        else
        {
            return output;
        }
    }

    private void form_keyDown(object sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            if (loadedBudget != null)
            {
                loadedBudget.Save();
                RefreshLayout();
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Z)
        {
            if (loadedBudget != null)
            {
                loadedBudget.Undo();
                RefreshLayout();
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.Y)
        {
            if (loadedBudget != null)
            {
                loadedBudget.Redo();
                RefreshLayout();
            }
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }

    private void mainScreen_deleteButton_click(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        Button senderButton = sender as Button;
        loadedBudget.Remove((int)senderButton.Tag);
        RefreshLayout();
    }
    private void mainScreen_amountBox_keyPress(object sender, KeyPressEventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        if (e.KeyChar == (char)Keys.Enter)
        {
            TextBox senderTextBox = sender as TextBox;
            double value = StringToAmount(senderTextBox.Text);
            if (!double.IsNaN(value))
            {
                loadedBudget.SetAmount((int)senderTextBox.Tag, value);
            }
            e.Handled = true;
            this.ActiveControl = null;
            RefreshLayout();
        }
    }
    private void mainScreen_amountBox_leave(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        TextBox senderTextBox = sender as TextBox;
        double value = StringToAmount(senderTextBox.Text);
        if (!double.IsNaN(value))
        {
            loadedBudget.SetAmount((int)senderTextBox.Tag, value);
        }
        RefreshLayout();
    }
    private void mainScreen_descriptionBox_keyPress(object sender, KeyPressEventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        if (e.KeyChar == (char)Keys.Enter)
        {
            TextBox senderTextBox = sender as TextBox;
            string value = senderTextBox.Text;
            loadedBudget.SetDescription((int)senderTextBox.Tag, value);
            e.Handled = true;
            RefreshLayout();
        }
    }
    private void mainScreen_descriptionBox_leave(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        TextBox senderTextBox = sender as TextBox;
        string value = senderTextBox.Text;
        loadedBudget.SetDescription((int)senderTextBox.Tag, value);
        RefreshLayout();
    }
    private void mainScreen_mouseWheel(object sender, MouseEventArgs e)
    {
        scrollOffset -= e.Delta / 120;
        RefreshLayout();
    }
    private void mainScreen_newButton_click(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        loadedBudget.Add(0.00, "");
        RefreshLayout();
    }
    private void mainScreen_saveButton_click(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        loadedBudget.Save();
        RefreshLayout();
    }
    private void mainScreen_undoButton_click(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        loadedBudget.Undo();
        RefreshLayout();
    }
    private void mainScreen_redoButton_click(object sender, EventArgs e)
    {
        if (loadedBudget == null)
        {
            return;
        }
        loadedBudget.Redo();
        RefreshLayout();
    }

    private void fileScreen_newButton_click(object sender, EventArgs e)
    {
        string filePath = null;
        using (SaveFileDialog saveFileDialog = new SaveFileDialog())
        {
            saveFileDialog.FileName = "New Budget";
            saveFileDialog.Filter = "EZB Files|*.ezb";
            saveFileDialog.Title = "Save EZB File As";

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            filePath = saveFileDialog.FileName;
        }
        string password = Interaction.InputBox("Input budget file password:", "");
        loadedBudget = EzBudget.New(filePath, password);
        loadedBudget.Save();
        RefreshLayout();
    }
    private void fileScreen_openButton_click(object sender, EventArgs e)
    {
        string filePath = null;
        using (OpenFileDialog openFileDialog = new OpenFileDialog())
        {
            openFileDialog.Filter = "EZB Files|*.ezb";
            openFileDialog.Title = "Open EZB File";

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            filePath = openFileDialog.FileName;
        }
        string password = Interaction.InputBox("Input budget file password:", "");
        loadedBudget = EzBudget.Load(filePath, password);
        if (loadedBudget == null)
        {
            MessageBox.Show("The password entered was incorrect.", "Incorrect Password", MessageBoxButtons.OK);
        }
        RefreshLayout();
    }

    private void RefreshLayout()
    {
        if (loadedBudget == null)
        {
            mainScreen_header.Control.Text = "EzBudget 1.0";

            fileScreen.Control.Enabled = true;
            fileScreen.Control.Visible = true;
            mainScreen.Control.Enabled = false;
            mainScreen.Control.Visible = false;
        }
        else
        {
            if (loadedBudget.GetHasChanges())
            {
                mainScreen_header.Control.Text = "EzBudget 1.0 *";
            }
            else
            {
                mainScreen_header.Control.Text = "EzBudget 1.0";
            }

            fileScreen.Control.Enabled = false;
            fileScreen.Control.Visible = false;
            mainScreen.Control.Enabled = true;
            mainScreen.Control.Visible = true;

            if (loadedBudget.Count() == 0)
            {
                scrollOffset = 0;
            }
            else if (scrollOffset < 0)
            {
                scrollOffset = 0;
            }
            else if (scrollOffset >= loadedBudget.Count())
            {
                scrollOffset = loadedBudget.Count() - 1;
            }

            for (int i = 0; i < 8; i++)
            {
                if (i + scrollOffset < 0 || i + scrollOffset >= loadedBudget.Count())
                {
                    mainScreen_transactions[i].container.Control.Enabled = false;
                    mainScreen_transactions[i].container.Control.Visible = false;
                }
                else
                {
                    mainScreen_transactions[i].container.Control.Enabled = true;
                    mainScreen_transactions[i].container.Control.Visible = true;
                    mainScreen_transactions[i].amountBox.Control.Tag = i + scrollOffset;
                    mainScreen_transactions[i].amountBox.Control.Text = AmountToString(loadedBudget.GetAmount(i + scrollOffset));
                    mainScreen_transactions[i].descriptionBox.Control.Tag = i + scrollOffset;
                    mainScreen_transactions[i].descriptionBox.Control.Text = loadedBudget.GetDescription(i + scrollOffset);
                    mainScreen_transactions[i].deleteButton.Control.Tag = i + scrollOffset;
                }
            }
        }
    }
}