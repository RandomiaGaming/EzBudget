using System;
using System.Windows.Forms;

public static class Program
{
    // New From Chase Export Button
    // Also Bar graph by company name button
    [STAThread]
    public static void Main(string[] args)
    {
        MicroServiceBClient.Init();
        MicroServiceCClient.Init();
        MicroServiceDClient.Init();

        EzBudgetForm ezBudget = new EzBudgetForm();
        Application.Run(ezBudget);

        MicroServiceBClient.Exit();
        MicroServiceCClient.Exit();
        MicroServiceDClient.Exit();
    }
}