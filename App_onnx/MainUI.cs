using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App_onnx
{
    public partial class MainUI1:ObservableObject
    {
        [ObservableProperty]
        string _InputText;
        [RelayCommand]
        void Chat()
        {

        }
    }
}
