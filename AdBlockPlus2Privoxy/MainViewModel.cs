using AdBlockPlus2Privoxy.Classes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AdBlockPlus2Privoxy
{
    public class MainViewModel : ViewModelBase
    {
        private string _abpFilePath;
        public string AbpFilePath
        {
            get
            {
                return _abpFilePath;
            }
            set
            {
                if (_abpFilePath != value)
                {
                    _abpFilePath = value;
                    OnPropertyChanged("AbpFilePath");
                    convert();
                }
            }
        }

        private string _actionsText;
        public string ActionsText
        {
            get
            {
                return _actionsText;
            }
            set
            {
                if (_actionsText != value)
                {
                    _actionsText = value;
                    OnPropertyChanged("ActionsText");
                }
            }
        }

        private string _filtersText;
        public string FiltersText
        {
            get
            {
                return _filtersText;
            }
            set
            {
                if (_filtersText != value)
                {
                    _filtersText = value;
                    OnPropertyChanged("FiltersText");
                }
            }
        }

        private void convert()
        {
            var abp = new AdBlockPlus();
            abp.Load(_abpFilePath);
            var privoxy = new Privoxy();
            privoxy.GenerateFiles(abp);
            ActionsText = privoxy.ActionsFile;
            FiltersText = privoxy.FiltersFile;
            File.WriteAllText("user.action", ActionsText);
            File.WriteAllText("user.filter", FiltersText);
        }

    }
}
