using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App_gguf
{
    public partial class MainUI:ObservableObject
    {
        [ObservableProperty]
        string _User = "";

        [RelayCommand]
        void Send()
        {

        }
    }
}
