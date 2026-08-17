using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Sampling;
using Microsoft.UI.Xaml.Documents;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace App_gguf
{
    public partial class MainUI : ObservableObject
    {
        public ObservableCollection<History> Historys { get; set; } = [];
        public async Task New()
        {
        }
        [ObservableProperty]
        public partial bool IsLoaded { get; set; } = false;
        [RelayCommand]
        async Task Send()
        {


        }


    }



    public partial class History : ObservableObject
    {
        public enum Role
        {
            AI,
            User
        }
        [ObservableProperty]
        public partial string Message { set; get; } = "";
    }
}
