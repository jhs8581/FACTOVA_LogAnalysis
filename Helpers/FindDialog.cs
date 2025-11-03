using System.Windows;
using FACTOVA_LogAnalysis.Services;

namespace FACTOVA_LogAnalysis.Helpers
{
    public class FindDialog : Window
    {
        public FindDialog()
        {
            // Basic implementation
            Title = "Find";
            Width = 300;
            Height = 150;
        }

        public void UpdateStatus(int current, int total)
        {
            // Implementation
        }
    }
}