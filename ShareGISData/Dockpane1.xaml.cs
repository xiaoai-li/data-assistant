﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Data;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Controls.Primitives;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Catalog;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

namespace DataAssistant
{
    /// <summary>
    /// Interaction logic for Dockpane1View.xaml
    /// </summary>
    public partial class Dockpane1View : UserControl
    {
        public static string AddinAssemblyLocation()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            return System.IO.Path.GetDirectoryName(
                              Uri.UnescapeDataString(
                                      new Uri(asm.CodeBase).LocalPath));
        }
        public static string getXmlFileName()
        {
            if (_filename != null)
                return _filename;
            else
                return "";
        }
        public static System.Xml.XmlDocument getXmlDocument()
        {
            return _xml;
        }
        public static string getNoneFieldName()
        {
            return _noneField;
        }
        public void setXmlFileName(string fname)
        {
            // set to default/current value if null
            if(fname != null)
                _filename = fname;
            if (this.FileName.Text != _filename)
            {
                this.FileName.Text = _filename;
                copyXml(_filename,_revertname);
            }
        }
        private static string _filename;// = System.IO.Path.Combine(AddinAssemblyLocation(), "ConfigData.xml");
        System.Xml.XmlNodeList _datarows;

        string fieldXPath = "/SourceTargetMatrix/Fields/Field";
        static System.Xml.XmlDocument _xml = new System.Xml.XmlDocument();
        TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
        //public int concatSequence = 0;
        private List<string> _concat = new List<string> { };
        static string _noneField = "(None)";
        static string _spaceVal = "(space)";
        private bool _skipSelectionChanged = false;
        private int _selectedRowNum = -1;
        int _methodnum = -1;
        string _revertname = System.IO.Path.Combine(AddinAssemblyLocation(), "RevertFile.xml");

        public Dockpane1View()
        {
            InitializeComponent();
        }
        public void loadFile(string fname)
        {
            // load the selected file
            if (System.IO.File.Exists(fname))
            {
                setXmlFileName(fname);
                //_xml.Load(_filename);
                if(loadXml(_filename))
                {
                    this._skipSelectionChanged = true;
                    setXmlDataProvider(this.FieldGrid, fieldXPath);
                    this._skipSelectionChanged = false;
                    setDatasetUI();
                    _datarows = _xml.SelectNodes("//Data/Row");
                }
            }
        }
        private bool loadXml(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                MessageBox.Show(filename + " does not exist, please select a file");
                return false;
            }
            string xmlstr = System.IO.File.ReadAllText(filename);
            // Encode in UTF-8 byte array
            byte[] encodedString = Encoding.UTF8.GetBytes(xmlstr);
            // Put the byte array into a stream and rewind
            System.IO.MemoryStream ms = new System.IO.MemoryStream(encodedString);
            ms.Flush();
            ms.Position = 0;
            _xml.Load(ms);
            return true;
        }
        private void setDatasetUI()
        {
            SourceStack.IsEnabled = true;
            TargetStack.IsEnabled = true;
            ReplaceStack.IsEnabled = true;
            
            SourceStack.Visibility = System.Windows.Visibility.Visible;
            TargetStack.Visibility = System.Windows.Visibility.Visible;
            ReplaceStack.Visibility = System.Windows.Visibility.Visible;

            System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/Source");
            if (node == null)
                MessageBox.Show("There appears to be an issue in your Xml document, required element Datasets/Source is missing from the document.");
            else
                SourceLayer.Text = node.InnerText;

            node = _xml.SelectSingleNode("//Datasets/Target");
            if (node == null)
                MessageBox.Show("There appears to be an issue in your Xml document, required element Datasets/Target is missing from the document.");
            else
                TargetLayer.Text = node.InnerText;

            setXmlDataProvider(ReplaceField, "//TargetField/@Name");
            System.Xml.XmlNodeList nodes = _xml.SelectNodes("//Datasets/ReplaceBy");
            setReplaceValues(nodes);
            setPreviewValues(false);
            
        }
        private void setRevertButton()
        {
            if (RevertButton.Visibility != System.Windows.Visibility.Visible)
            {
                RevertButton.Visibility = System.Windows.Visibility.Visible;
                if (FileGrid.RowDefinitions[0].ActualHeight < 70)
                {
                    System.Windows.GridLength len = new System.Windows.GridLength(FileGrid.RowDefinitions[0].ActualHeight + 30);
                    FileGrid.RowDefinitions[0].Height = len;
                }
            }
        }
        private void setReplaceValues(System.Xml.XmlNodeList nodes)
        {
            if (nodes != null)
            {
                try
                {
                    bool hasValue = false;
                    string field = getReplaceValue(nodes, "FieldName");
                    string op = getReplaceValue(nodes, "Operator");
                    string value = getReplaceValue(nodes, "Value");
                    if (field != null)
                    {
                        setReplaceValue(ReplaceField, field);
                        hasValue = true;
                    }
                    if (op != null)
                    {
                        setReplaceValue(ReplaceOperator, op);
                        hasValue = true;
                    }
                    if (value != null)
                    {
                        ReplaceValue.Text = value;
                        hasValue = true;
                    }
                    if (hasValue == true)
                    {
                        ReplaceByCheckBox.IsChecked = true;
                        ReplaceByCheckBox_Checked(ReplaceByCheckBox, null);
                    }
                    else
                    {
                        clearReplaceValues();
                    }
                }
                catch { MessageBox.Show("Error setting replace"); }
            }
            else
            {
                clearReplaceValues();
            }

        }
        private void clearReplaceValues()
        {
            ReplaceField.SelectedIndex = -1;
            ReplaceOperator.SelectedIndex = -1;
            ReplaceValue.Text = "";
            ReplaceByCheckBox.IsChecked = false;
            System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/ReplaceBy");
            if (node != null)
            {
                node.RemoveAll();
                saveFieldGrid();
            }
            _skipSelectionChanged = true;
            ReplaceByCheckBox_Unchecked(ReplaceByCheckBox, null);
            _skipSelectionChanged = false;
        }

        private string getReplaceValue(System.Xml.XmlNodeList replace,string nodeName)
        {
            string txt = null;
            System.Xml.XmlNode node = replace[0].SelectSingleNode(nodeName);
            if (node != null)
                txt = node.InnerText;
            return txt;
        }
        private void setReplaceValue(ComboBox combo, string theval)
        {
            if (combo != null)
            {
                _skipSelectionChanged = true;
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    object obj = combo.Items.GetItemAt(i);
                    ComboBoxItem item = obj as ComboBoxItem;
                    if (item != null)
                    {
                        string comp = item.Content.ToString();
                        if (comp == theval)
                            combo.SelectedIndex = i;
                    }
                    else
                    {
                        System.Xml.XmlElement elem = obj as System.Xml.XmlElement;
                        if (elem != null)
                        {
                            string comp = elem.InnerText;
                            if (comp == theval)
                                combo.SelectedIndex = i;
                        }
                        else
                        {
                            System.Xml.XmlAttribute attr = obj as System.Xml.XmlAttribute;
                            if (attr != null)
                            {
                                string comp = attr.InnerText;
                                if (comp == theval)
                                    combo.SelectedIndex = i;
                            }

                        }
                    }
                }
                _skipSelectionChanged = false;
            }
        }
        private void saveFieldGrid()
        {
            if(this.IsInitialized)
            {
                XmlDataProvider dp = new XmlDataProvider();
                dp = this.FieldGrid.DataContext as XmlDataProvider;
                dp.IsAsynchronous = false;
                //setXmlFileName(FileName.Text);
                
                dp.Document.Save(getXmlFileName());
                setRevertButton();
            }           
        }
        private void setXmlDataProvider(object ctrl,string xpath)
        {
            XmlDataProvider dp = new XmlDataProvider();
            if (this.IsInitialized)
            {
                try
                {
                    dp.IsAsynchronous = false;
                    dp.Document = _xml;
                    dp.XPath = xpath;
                    DataGrid uictrl = ctrl as DataGrid;
                    if (uictrl == null)
                    {
                        ComboBox cbctrl = ctrl as ComboBox;
                        cbctrl.DataContext = dp;
                    }
                    else
                        uictrl.DataContext = dp;
                }
                catch
                {
                    MessageBox.Show("Error setting Xml data provider");
                }
            }
        }
        private void FieldGrid_Selected(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FieldGrid.SelectedIndex == -1 || FieldGrid == null)
                Methods.IsEnabled = false;
            else
                Methods.IsEnabled = true;
        }
        private void FieldGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Need to pull the current configuration values from the config, also need to set the correct panel as visible
            if (this._skipSelectionChanged || (_selectedRowNum == FieldGrid.SelectedIndex))
                return;
            if(FieldGrid.SelectedIndex == -1)
                return;
            _selectedRowNum = FieldGrid.SelectedIndex;
            var cfg = getConfigSettingsForField();
            int methodnum = setFieldSelectionValues(cfg); // just use the int for now.
            
            setPanelVisibility(methodnum);
        }
        private System.Xml.XmlNodeList getFieldNodes(int fieldnum)
        {
            System.Xml.XmlNodeList nodes = null;
            string xpath = "//Field[position()=" + fieldnum.ToString() + "]"; // Field grid position to set
            System.Xml.XmlNodeList nodelist = _xml.SelectNodes(xpath);
            if (nodelist != null && nodelist.Count == 1)
                return nodelist;
            else
                return nodes;
        }
        private System.Xml.XmlNodeList getSourceFieldNodes()
        {
            System.Xml.XmlNodeList nodes = null;
            if (FieldGrid.SelectedIndex == -1)
                return nodes;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            System.Xml.XmlNodeList fnodes = getFieldNodes(fieldnum);
            string sname = "";
            try
            {
                sname = fnodes.Item(0).SelectSingleNode("SourceName").InnerText;
            }
            catch
            {
                MessageBox.Show("Could not find SourceName element for field (row) number " + fieldnum.ToString());
                return nodes;
            }
            string xpath = "//SourceField[@Name='" + sname + "']"; // Source field values
            System.Xml.XmlNodeList nodelist = _xml.SelectNodes(xpath);
            if (nodelist != null && nodelist.Count == 1)
                return nodelist;
            else
                return nodes;
        }
        private System.Xml.XmlNodeList getSourceFields()
        {
            string xpath = "//SourceField"; // Source field values
            System.Xml.XmlNodeList nodelist = _xml.SelectNodes(xpath);
            return nodelist;
        }
        private string getSourceFieldName()
        {
            string fname = "None";
            if (FieldGrid.SelectedIndex == -1)
                return fname;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            var nodes = getFieldNodes(fieldnum);
            if (nodes.Count == 1 && nodes != null)
            {
                var node = nodes.Item(0).SelectSingleNode("SourceName");
                if (node != null)
                    fname = node.InnerText;
            }
            return fname;
        }
        private string getTargetFieldName()
        {
            string fname = "None";
            if (FieldGrid.SelectedIndex == -1)
                return fname;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            var nodes = getFieldNodes(fieldnum);
            if (nodes.Count == 1 && nodes != null)
            {
                var node = nodes.Item(0).SelectSingleNode("TargetName");
                if (node != null)
                    fname = node.InnerText;
            }
            return fname;
        }
        private int getConfigSettingsForField()
        {
            if (FieldGrid.SelectedIndex == -1)
                return -1;
            int num = -1;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            var nodes = getFieldNodes(fieldnum);
            if (nodes.Count == 1 && nodes != null)
            {
                try
                {
                    var node = nodes.Item(0).SelectSingleNode("Method");
                    for(int i=0;i<comboMethod.Items.Count;i++)
                    {
                        string val = comboMethod.Items.GetItemAt(i).ToString().Replace(" ","");
                        // special case to convert DefaultValue to SetValue
                        if (val.EndsWith("SetValue") && node.InnerText.ToString() == "DefaultValue")
                            num = i;
                        else if (val.EndsWith(node.InnerText.ToString()))
                            num = i;
                    }
                }
                catch
                { }
            }

            return num;
        }
        private int setFieldSelectionValues(int methodnum)
        {
            comboMethod.SelectedIndex = methodnum;
            setMethodVisibility(methodnum);
            _methodnum = methodnum;

            switch(methodnum){ // fill in the values for each stack panel
                case 0: // None
                    break;
                case 1: // Copy
                    break;
                case 2: // SetValue
                    Method2Value.Text = getPanelValue(2, "SetValue");
                    break;
                case 3: // ValueMap
                    setValueMapValues(3, getPanelValue(3, "ValueMap"));
                    break;
                case 4: // ChangeCase
                    setComboValue(4, getPanelValue(4, "ChangeCase"));
                    break;
                case 5: // Concatenate
                    _concat.Clear();
                    setSpaceVal(getPanelValue(5,"Separator"),Method5Value);
                    setConcatValues(); 
                    Method5.InvalidateArrange();
                    break;
                case 6: // Left
                    setSliderValue(6, getPanelValue(6, "Left"));
                    break;
                case 7: // Right
                    setSliderValue(7, getPanelValue(7, "Right"));
                    break;
                case 8: // Substring
                    setSubstringValues(getPanelValue(81, "Start"), getPanelValue(82, "Length"));
                    break;
                case 9: // Split
                    setSpaceVal(getPanelValue(91,"SplitAt"),Method91Value);
                    Method92Value.Text = getPanelValue(92, "Part");
                    break;
                case 10: // Conditional Value
                    setConditionValues();
                    break;
                case 11: // Domain Map
                    setDomainMapValues(11, getPanelValue(11, "DomainMap"));
                    break;

                    //case 11: // Expression
                    //Method11Value.Text = getPanelValue(11, "Expression");
                    //break;

            }

            return methodnum;
        }
        private void setMethodVisibility(int methodnum)
        {
            if (MethodControls == null || ! MethodControls.IsInitialized)
            {
                return;
            }
            //if (methodnum < 3)
            //{
            //    MethodControls.Visibility = System.Windows.Visibility.Hidden;
            //}
            //else
            MethodControls.Visibility = System.Windows.Visibility.Visible;
            //PreviewText.Text = "";
        }

        private string getPanelValue(int methodnum,string nodename)
        {
            if (FieldGrid.SelectedIndex == -1)
                return "error!";
            string theval = "";
            int fieldnum = FieldGrid.SelectedIndex + 1;
            var nodes = getFieldNodes(fieldnum);
            if (nodes.Count == 1)
            {
                try
                {
                    var node = nodes.Item(0).SelectSingleNode(nodename);
                    if (node == null && nodename == "SetValue")
                    {
                        node = nodes.Item(0).SelectSingleNode("DefaultValue");
                    }
                    if(node != null)
                        theval = node.InnerText.ToString();
                }
                catch
                { }
            }
            return theval;
        }
        private void setComboValue(int combonum,string theval)
        {
            string comboname = "Method" + combonum + "Combo";
            Object ctrl = this.FindName(comboname);
            ComboBox comb = ctrl as ComboBox;
            if (comb != null)
            {
                for (int i = 0; i < comb.Items.Count;i++ )
                {
                    string comp = comb.Items.GetItemAt(i).ToString();
                    if (comp.EndsWith(theval))
                        comb.SelectedIndex = i;
                }
            }
        }
        private void setSliderValue(int combonum, string theval)
        {
            string name = "Method" + combonum + "Slider";
            Object ctrl = this.FindName(name);
            Slider slide = ctrl as Slider;
            if (slide != null)
            {
                int val;
                Int32.TryParse(theval, out val);
                slide.Value = val;
            }
        }

        private void setValueMapValues(int combonum, string nodename)
        {
            if (FieldGrid.SelectedIndex == -1)
                return;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            System.Xml.XmlNodeList nodes = getFieldNodes(fieldnum);
            System.Xml.XmlNodeList snodes;
            System.Xml.XmlNodeList tnodes;
            tnodes = nodes[0].SelectNodes("ValueMap/tValue");
            snodes = nodes[0].SelectNodes("ValueMap/sValue");

            string name = "Method" + combonum + "Grid";
            Object ctrl = this.FindName(name);
            DataGrid grid = ctrl as DataGrid;
            if (grid == null)
                return;

            grid.Items.Clear();
            for (int i = 0; i < snodes.Count; i++)
            {
                System.Xml.XmlNode sourcenode = snodes.Item(i);
                string sourcename = sourcenode.InnerText;
                System.Xml.XmlNode targetnode = tnodes.Item(i);
                string targetname = targetnode.InnerText;

                grid.Items.Add(new ValueMapRow() { Source = sourcename, Target = targetname });
            }
            System.Xml.XmlNode othnode = nodes[0].SelectSingleNode("ValueMap/Otherwise");
            if(othnode != null)
                Method3Otherwise.Text = othnode.InnerText;
            else Method3Otherwise.Text = "";
            
            if (grid.Items.Count > 0)
                ValueMapRemove.IsEnabled = true;
            else
                ValueMapRemove.IsEnabled = false;
        }

        private void setDomainMapValues(int combonum, string nodename)
        {
            if (FieldGrid.SelectedIndex == -1)
                return;
            int fieldnum = FieldGrid.SelectedIndex + 1;
            System.Xml.XmlNodeList nodes = getFieldNodes(fieldnum);
            System.Xml.XmlNodeList sValueNodes, sLabelNodes, tValueNodes, tLabelNodes;
            
            //<Method>ValueMap</Method>
            //<ValueMap>
            //  <sValue>1</sValue>
            //  <sLabel>A things</sLabel>
            //  <tValue>12</tValue>
            //  <tLabel>12 things</tLabel>
            //  <sValue>2</sValue>
            //  <sLabel>2 things</sLabel>
            //  <tValue>22</tValue>
            //  <tLabel>22 things</tLabel>
            //  <Otherwise>
            //  </Otherwise>
            //</ValueMap>
            tValueNodes = nodes[0].SelectNodes("DomainMap/tValue");
            tLabelNodes = nodes[0].SelectNodes("DomainMap/tLabel");

            sValueNodes = nodes[0].SelectNodes("DomainMap/sValue");
            sLabelNodes = nodes[0].SelectNodes("DomainMap/sLabel");

            string name = "Method" + combonum + "Grid";
            Object ctrl = this.FindName(name);
            DataGrid grid = ctrl as DataGrid;
            if (grid == null)
                return;

            List<ComboData> combo = getDomainValues(SourceLayer.Text, getSourceFieldName());

            grid.Items.Clear();
            for (int i = 0; i < tValueNodes.Count; i++)
            {
                
                System.Xml.XmlNode sourcenode = sValueNodes.Item(i);
                if (sourcenode == null)
                    return;
                string sVal = sourcenode.InnerText;
                int selected = -1;
                string sTooltip = "";
                for(int s=0;s<combo.Count;s++)
                {
                    if (combo[s].Id.ToString().Equals(sVal))
                        selected = s;
                }
                sourcenode = sLabelNodes.Item(i);
                sTooltip = sourcenode.InnerText;

                System.Xml.XmlNode targetnode = tValueNodes.Item(i);
                string tVal = targetnode.InnerText;
                targetnode = tLabelNodes.Item(i);
                string tTooltip = targetnode.InnerText;

                grid.Items.Add(new DomainMapRow() { Source = combo, SourceSelectedItem = selected, SourceTooltip=sTooltip, TargetTooltip=tTooltip, Target = tVal });
            }
        }

        private void setSpaceVal(string separator,TextBox txt)
        {
            if (txt != null && separator != txt.Text)
            {
                txt.Text = separator.Replace(_spaceVal," ");
            }
        }
        private void setConcatValues()
        {
            System.Xml.XmlNodeList sourcenodes = getSourceFields();

            int fieldnum = FieldGrid.SelectedIndex + 1;
            DataGrid grid = Method5Grid;
            if (grid == null)
                return;
            System.Xml.XmlNodeList cnodes = getFieldNodes(fieldnum);
            try { cnodes = cnodes[0].SelectNodes("cFields/cField"); }
            catch { }

            grid.Items.Clear();
            if (_concat.Count == 0 && cnodes != null)
            {
                for (int c = 0; c < cnodes.Count; c++)
                {
                    // assume source nodes written in sequence order... and only checked items in the xml
                    System.Xml.XmlNode cnode = cnodes.Item(c);
                    string cname = cnode.SelectSingleNode("Name").InnerText;
                    if (cname != _noneField)
                    {
                        grid.Items.Add(new ConcatRow() { Checked = true, Name = cname });
                        _concat.Add(cname);
                    }
                }
            }
            else
            {
                for (int c = 0; c < _concat.Count; c++)
                {
                    // if there are items in the concat list use them in order
                    grid.Items.Add(new ConcatRow() { Checked = true, Name = _concat[c] });
                }
            }
            if(_concat.Count > 0)
                Method5ClearAll.IsEnabled = true;

            for (int i = 0; i < sourcenodes.Count; i++)
            {
                // add the unchecked items in row order
                System.Xml.XmlNode sourcenode = sourcenodes.Item(i);
                string sourcename = sourcenode.Attributes.GetNamedItem("Name").InnerText;
                bool found = false;
                for (int c = 0; c < _concat.Count; c++)
                {
                    // look for a matching field that has a checked value, don't add if checked.
                    string cname = _concat[c];
                    if (cname == sourcename)
                        found = true;
                }
                if (!found && sourcename != _noneField)
                {
                    try
                    {
                        grid.Items.Add(new ConcatRow() { Checked = found, Name = sourcename });
                    }
                    catch
                    {
                        MessageBox.Show("Error setting checkbox values");
                    }
                }
            }
            grid.Items.Refresh();           
        }

        private void setSubstringValues(string start, string length)
        {
            try
            {
                setSliderValue(81, start);
                System.Xml.XmlNodeList source = getSourceFieldNodes();
                int max = Int32.Parse(source.Item(0).Attributes.GetNamedItem("Length").InnerText);
                Method82Slider.Maximum = max;
                setSliderValue(82, length);
            }
            catch
            { }
        }
 
        private void setConditionValues()
        {
            string source = getPanelValue(101, "SourceName");
            if(source != _noneField)
                Method10Label.Content = "If (" + source + ") is";
            else
                Method10Label.Content = "If ";
            string iff = getPanelValue(101, "If");
            string oper = getPanelValue(10, "Oper");
            for (int i = 0; i < Method10Value.Items.Count; i++)
            {
                ComboBoxItem item = Method10Value.Items[i] as ComboBoxItem;
                if(item.Content.ToString() == oper)
                    Method10Value.SelectedIndex = i;
            }
            Method101Value.Text = iff;
            Method102Value.Text = getPanelValue(102, "Then");
            Method103Value.Text = getPanelValue(103, "Else");

        }
        private void FieldGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
        }
        
        private void comboMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this._skipSelectionChanged)
                return;
            setFieldSelectionValues(comboMethod.SelectedIndex);
            setPanelVisibility(comboMethod.SelectedIndex);
        }

        private void setPanelVisibility(int index)
        {
            string methnum = index.ToString();

            for (int i = 0; i < comboMethod.Items.Count; i++)
            {
                if (i == index)
                {
                    string method = "Method" + methnum;
                    Object ctrl = this.FindName(method);
                    StackPanel panel = ctrl as StackPanel;
                    if (panel != null)
                    {
                        try
                        {
                            panel.Visibility = System.Windows.Visibility.Visible;
                            panel.InvalidateArrange();
                            panel.UpdateLayout();
                        }
                        catch { }
                    }
                }
                else
                {
                    string method = "Method" + i;
                    Object ctrl = this.FindName(method);
                    StackPanel panel = ctrl as StackPanel;
                    if (panel != null)
                    {
                        try
                        {
                            panel.Visibility = System.Windows.Visibility.Hidden;
                            panel.InvalidateArrange();
                            panel.UpdateLayout();
                        }
                        catch { }
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Dockpane1ViewModel.doHide();
        }
        public bool setFiles()
        {
            //string dataFolder;
            //dataFolder = "this";
            return true;
        }

        private void Method5ClearAll_Click(object sender, RoutedEventArgs e)
        {
            setAllConcat(false,5);
        }

        private void ConcatAll_Click(object sender, RoutedEventArgs e)
        {
            setAllConcat(true, 5);
        }
        private void setAllConcat(bool val,int combonum)
        {
            System.Xml.XmlNodeList sourcenodes = getSourceFields();

            string name = "Method" + combonum + "Grid";
            object ctrl = this.FindName(name);
            DataGrid grid = ctrl as DataGrid;
            if (grid == null)
                return;

            grid.Items.Clear();
            _concat.Clear();
            for (int i = 0; i < sourcenodes.Count; i++)
            {
                System.Xml.XmlNode sourcenode = sourcenodes.Item(i);
                string sourcename = sourcenode.Attributes.GetNamedItem("Name").InnerText;
                if (val == true && sourcename != _noneField)
                    _concat.Add(sourcename);
                if (sourcename != _noneField)
                    grid.Items.Add(new ConcatRow() { Checked = val, Name = sourcename});
            }
            if (val == false)
                Method5ClearAll.IsEnabled = false;
        }

        private void Method5Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
        private void Method5Check_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox check = sender as CheckBox;
            if (Method5Grid.SelectedIndex == -1)
                return;

            if(check != null)
            {
                for (int i = 0; i < Method5Grid.Items.Count; i++)
                {
                    if (i == Method5Grid.SelectedIndex)
                    {
                        object item = Method5Grid.Items.GetItemAt(i);
                        ConcatRow row = item as ConcatRow;
                        if (row != null)
                        {
                            bool chk = (check.IsChecked.HasValue) ? check.IsChecked.Value : false;
                            row.Checked = chk;
                            bool present = false;
                            for (int c = 0; c < _concat.Count; c++)
                            {
                                if ( Equals(row.Name,_concat[c]))
                                    present = true;
                            }
                            if (chk && ! present)
                            {
                                _concat.Add(row.Name);
                                setConcatValues();
                            }
                            else if (! chk && present)
                            {
                                _concat.Remove(row.Name);
                                setConcatValues();
                            }  
                        }
                    }
                }

            }
        }



        //private DataGridCell GetCell(DataGrid grid, DataGridRow row, int column)
        //{
        //    if (row != null)
        //    {
        //        DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(row);

        //        if (presenter == null)
        //        {
        //            grid.ScrollIntoView(row, grid.Columns[column]);
        //            presenter = GetVisualChild<DataGridCellsPresenter>(row);
        //        }

        //        DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(column);
        //        return cell;
        //    }
        //    return null;
        //}
        //public T GetVisualChild<T>(Visual parent) where T : Visual
        //{
        //    T child = default(T);
        //    int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
        //    for (int i = 0; i < numVisuals; i++)
        //    {
        //        Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
        //        child = v as T;
        //        if (child == null)
        //        {
        //            child = GetVisualChild<T>(v);
        //        }
        //        if (child != null)
        //        {
        //            break;
        //        }
        //    }
        //    return child;
        //}

        private void Method3Target_TextChanged(object sender, TextChangedEventArgs e)
        {
            Method3TextChanged(sender, "Target");
        }
        private void Method3Source_TextChanged(object sender, TextChangedEventArgs e)
        {
            Method3TextChanged(sender,"Source");
        }
        private void Method3TextChanged(object sender,string sourcetarget)
        {
            TextBox txt = sender as TextBox;

            if(Method3Grid.SelectedIndex == -1)
                return;

            if (txt != null)
            {
                for (int i = 0; i < Method3Grid.Items.Count; i++)
                {
                    if (i == Method3Grid.SelectedIndex)
                    {
                        object item = Method3Grid.Items.GetItemAt(i);
                        ValueMapRow row = item as ValueMapRow;
                        if (row != null)
                        {
                            if(sourcetarget == "Source")
                                row.Source = txt.Text;
                            else if (sourcetarget == "Target")
                                row.Target = txt.Text;
                        }
                    }
                }

            }

        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.OpenFileDialog())
            {
                dlg.Filter = "Data Loading Assistant Xml files|*.xml";//.Description = "Browse for a Source-Target File (.xml)";
                dlg.Multiselect = false;
                System.Windows.Forms.DialogResult result = dlg.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    //this.FileName.Text = dlg.FileName;
                    loadFile(dlg.FileName);
                }
            }

        }

        private void FileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (getXmlFileName() != txt.Text)
            {
                setXmlFileName(txt.Text);
                loadFile(txt.Text);
            }
        }

        //private DataRowView rowBeingEdited = null;

        //private void Method3Grid_CurrentCellChanged(object sender, EventArgs e)
        //{

        //    DataGrid dataGrid = sender as DataGrid;
        //    DataGridRow row = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(0);
        //    DataGridCell rowColumn = dataGrid.Columns[0].GetCellContent(row).Parent as DataGridCell;

        //    DataGridCell cell = GetCell(Method3Grid, row, 1);
        //    var cellValue = rowColumn.Content;
        //    if (cellValue != null)
        //    {
        //        ValueMapRow vmrow = rowColumn.Content as ValueMapRow;
        //        //string currValue = vmrow.Target;
        //    }
        //}
        //private void Method3Grid_CellEditEndingx(object sender, DataGridCellEditEndingEventArgs e)
        //{
        //    DataRowView rowView = e.Row.Item as DataRowView;
        //    rowBeingEdited = rowView;

        //}
        //private bool isManualEditCommit;
        //private void Method3Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        //{
        //    if (!isManualEditCommit)
        //    {
        //        isManualEditCommit = true;
        //        DataGrid grid = (DataGrid)sender;
        //        grid.CommitEdit(DataGridEditingUnit.Row, true);
        //        isManualEditCommit = false;
        //    }
        //}

        private void ValueMapAdd_Click(object sender, RoutedEventArgs e)
        {
            Method3Grid.Items.Add(new ValueMapRow() { Source = "", Target = "" });
            Method3Grid.InvalidateArrange();
            ValueMapRemove.IsEnabled = true;
        }

        private void ValueMapRemove_Click(object sender, RoutedEventArgs e)
        {
            if (Method3Grid.SelectedIndex > -1 && Method3Grid.Items.Count > 0)
                Method3Grid.Items.RemoveAt(Method3Grid.SelectedIndex);
        }

        private void Method5Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (txt.Text.IndexOf(" ") > -1)
                txt.Text = txt.Text.Replace(" ", _spaceVal);

        }
        private void Method91Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (txt.Text.IndexOf(" ") > -1)
                txt.Text = txt.Text.Replace(" ", _spaceVal);
        }

        private void SourceField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (this._skipSelectionChanged || comboBox.IsLoaded == false)
                return;
            bool doSave = false;
            
            if (((ComboBox)sender).IsLoaded && (e.AddedItems.Count > 0 || e.RemovedItems.Count > 0) && FieldGrid.SelectedIndex != -1)
            { // disregard SelectionChangedEvent fired on population from binding
                
                for (Visual visual = (Visual)sender; visual != null; visual = (Visual)VisualTreeHelper.GetParent(visual))
                { // Traverse tree to find correct selected item
                    if (visual is DataGridRow)
                    {
                        DataGridRow row = visual as DataGridRow;
                        object val = row.Item;
                        System.Xml.XmlElement xml = val as System.Xml.XmlElement;
                        if(xml != null)
                        {
                            try
                            {
                                string nm = xml.GetElementsByTagName("TargetName")[0].InnerText;
                                string xmlname = "";
                                try
                                {
                                    xmlname = _xml.SelectSingleNode("//Field[position()=" + (_selectedRowNum + 1).ToString() + "]/TargetName").InnerText;
                                }
                                catch { }
                                if (nm == xmlname)
                                {
                                    doSave = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }            
            
            int fieldnum = FieldGrid.SelectedIndex + 1;
            var nodes = getFieldNodes(fieldnum);
            if (nodes != null && comboBox != null && comboBox.SelectedValue != null && doSave == true) 
            {
                try
                {
                    string selected = comboBox.SelectedValue.ToString();
                    this._skipSelectionChanged = true;
                    if (nodes.Count == 1)
                    {
                        // source field selection should change to Copy
                        var node = nodes.Item(0).SelectSingleNode("Method");
                        var nodeField = nodes.Item(0).SelectSingleNode("SourceName");
                        if (selected == _noneField && comboMethod.SelectedIndex != 0)
                        {
                            node.InnerText = "None";
                            nodeField.InnerText = selected;
                            comboMethod.SelectedIndex = 0;
                            saveFieldGrid();
                        }
                        else if (selected != _noneField)
                        {
                            node.InnerText = "Copy";
                            nodeField.InnerText = selected;
                            comboMethod.SelectedIndex = 1;
                            saveFieldGrid();
                        }
                        _selectedRowNum = fieldnum;
                        this._skipSelectionChanged = false;
                    }
                }
                catch
                { }
            }
        }

        private void TargetLayer_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/Target");
            if (node != null && node.InnerText != txt.Text)
            {
                node.InnerText = txt.Text;
                saveFieldGrid();
            }
        }

        private void SourceLayer_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (txt != null)
            {
                System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/Source");
                if (node != null && node.InnerText != txt.Text)
                {
                    node.InnerText = txt.Text;
                    saveFieldGrid();
                }
            }
        }

        private void ReplaceField_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo != null && combo.SelectedIndex != -1)
            {
                System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/ReplaceBy/FieldName");
                if (node == null || node.InnerText != combo.SelectionBoxItem.ToString())
                    if(_skipSelectionChanged != true)
                        updateReplaceNodes();
            }
        }

        private void ReplaceOperator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = sender as ComboBox;
            if (combo != null && combo.SelectedIndex != -1)
            {
                System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/ReplaceBy/Operator");
                if (node == null || node.InnerText != combo.SelectionBoxItem.ToString())
                    if (_skipSelectionChanged != true)
                        updateReplaceNodes();
            }
        }
        private void ReplaceValue_SelectionChanged(object sender, TextChangedEventArgs e)
        {
            TextBox txt = sender as TextBox;
            if (txt != null && txt.Text != "")
            {
                System.Xml.XmlNode node = _xml.SelectSingleNode("//Datasets/ReplaceBy/Value");
                if (node == null || node.InnerText != txt.Text)
                    if (_skipSelectionChanged != true)
                        updateReplaceNodes();
            }
        }

        public static bool runTransform(string xmlPath, string xsltPath, string outputPath, XsltArgumentList argList)
        {
            XmlTextReader reader = null;
            XmlWriter writer = null;
            try
            {
                XsltSettings xslt_set = new XsltSettings();
                xslt_set.EnableScript = true;
                xslt_set.EnableDocumentFunction = true;

                // Load the XML source file.
                reader = new XmlTextReader(xmlPath);

                // Create an XmlWriter.
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = new UTF8Encoding();
                settings.OmitXmlDeclaration = false;

                writer = XmlWriter.Create(outputPath, settings);

                XslCompiledTransform xslt = new XslCompiledTransform();
                xslt.Load(xsltPath, xslt_set, new XmlUrlResolver());
                if (argList == null)
                    xslt.Transform(reader, writer);
                else
                    xslt.Transform(reader, argList, writer);
                reader.Close();
                writer.Close();

                return true;
            }
            catch (Exception err)
            {
                try
                {
                    if (reader != null)
                        reader.Close();
                    if (writer != null)
                        writer.Close();
                    throw (err);
                }
                catch (Exception err2)
                {
                    MessageBox.Show(err2.ToString());
                    return false;
                }
            }
        }
        private bool copyXml(string fName1, string fName2)
        {
            System.IO.FileInfo fp1 = new System.IO.FileInfo(fName1);
            try
            {
                fp1.CopyTo(fName2, true);
            }
            catch (Exception e)
            {
                string errStr = e.Message;
                return false;
            }
            return true;
        }

        private void ReplaceByCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            if (chk != null)
            {
                //ReplaceStackSettings.Height = 110;
                ReplaceStackSettings.Visibility = System.Windows.Visibility.Visible;
                System.Windows.GridLength len = new System.Windows.GridLength(110);
                //System.Xml.XmlNodeList nodes = _xml.SelectNodes("//Datasets/ReplaceBy");
                //setReplaceValues(nodes);
                FileGrid.RowDefinitions[3].Height = len;
                FileGrid.InvalidateArrange();
                FileGrid.UpdateLayout();
            }

        }

        private void ReplaceByCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox chk = sender as CheckBox;
            if (chk != null)
            {
                if(!_skipSelectionChanged)
                    setReplaceValues(null);
                ReplaceStackSettings.Visibility = System.Windows.Visibility.Hidden;
                System.Windows.GridLength len = new System.Windows.GridLength(0);
                FileGrid.RowDefinitions[3].Height = len;
                FileGrid.InvalidateArrange();
                FileGrid.UpdateLayout();
            }

        }

        private void RevertButton_Click(object sender, RoutedEventArgs e)
        {
            if (System.Windows.Forms.MessageBox.Show("Are you sure you want to re-open this file?", "Revert/Re-Open File", System.Windows.Forms.MessageBoxButtons.YesNo) == System.Windows.Forms.DialogResult.Yes)
            {
                copyXml(_revertname, _filename);
                loadFile(_filename);
            }

        }


        private void ImportTarget_Click(object sender, RoutedEventArgs e)
        {
            // Need to get the domain values from the target dataset - NB do domain but could also be based on values if no domain
            
            // = new List<ComboData>();
            List<ComboData> domainValues = getDomainValues(this.TargetLayer.Text, getTargetFieldName());
            List<ComboData> sourceValues = getDomainValues(this.SourceLayer.Text, getSourceFieldName());

            Method11Grid.Items.Clear();
            for (int i = 0; i < domainValues.Count; i++)
            {
                ComboData domainValue = domainValues[i];
                string target = domainValue.Id;
                int selected = -1;
                for (int s = 0; s < sourceValues.Count; s++)
                {
                    string dvalue = sourceValues[s].Id;
                    if (target.Equals(dvalue))
                        selected = s;
                }
                Method11Grid.Items.Add(new DomainMapRow() { Source = domainValues, SourceSelectedItem = selected, SourceTooltip=domainValues[selected].Tooltip, Target = domainValue.Id, TargetTooltip = domainValue.Value });
            }
            Method11Grid.InvalidateArrange();
        }

        private void ImportSource_Click(object sender, RoutedEventArgs e)
        {
            // replace source domain values
            List<ComboData> domainValues = new List<ComboData>();
            domainValues = getDomainValues(this.SourceLayer.Text, getSourceFieldName());
            Method11Grid.Items.Clear();
            for (int s = 0; s < domainValues.Count; s++)
            {
                Method11Grid.Items.Add(new DomainMapRow() { Source = domainValues, SourceSelectedItem = s, SourceTooltip = domainValues[s].Tooltip, Target = "" });
            }
        }

        public List<ComboData> getDomainValues(string dataset, string fieldName)
        {
            List<ComboData> domainValues = new List<ComboData>(); // *** does not work for layers currently... grab dataset from before the "." in field name... 
            string table = dataset.Substring(dataset.LastIndexOf("\\")+1);
            string db = dataset.Substring(0,dataset.LastIndexOf("\\" + table));
            if(fieldName.Equals(_noneField))
            {
                MessageBox.Show("No field to map");
                return domainValues;
            }
            try
            {
                using (ArcGIS.Core.Data.Geodatabase geodatabase = new Geodatabase(db))
                using (ArcGIS.Core.Data.Table tab = geodatabase.OpenDataset<ArcGIS.Core.Data.Table>(table))
                {
                    ArcGIS.Core.Data.TableDefinition def = tab.GetDefinition();
                    IReadOnlyList<ArcGIS.Core.Data.Field> fields = def.GetFields();
                    ArcGIS.Core.Data.Field thefield = fields.First(field => field.Name.ToLower() == fieldName.ToLower());
                    Domain domain = thefield.GetDomain();
                    if (domain is CodedValueDomain)
                    {
                        var codedValueDomain = domain as CodedValueDomain;
                        SortedList<object, string> codedValuePairs = codedValueDomain.GetCodedValuePairs();
                        //IEnumerable<KeyValuePair<object, string>> filteredPairs = codedValuePairs.Where(pair => Convert.ToDouble(pair.Key) > 20.0d);
                        for (int i = 0; i < codedValuePairs.Count; i++)
                        {
                            //string str = codedValuePairs.ElementAt(i).Key.ToString() + " | " + codedValuePairs.ElementAt(i).Value.ToString();
                            ComboData item = new ComboData();
                            item.Id = codedValuePairs.ElementAt(i).Key.ToString();
                            item.Value = codedValuePairs.ElementAt(i).Value.ToString();
                            item.Tooltip = codedValuePairs.ElementAt(i).Value.ToString();
                            domainValues.Add(item);
                        }
                    }
                }
            }
            catch {
                MessageBox.Show("Unable to retrieve domain values for " + fieldName);
                return domainValues;
            }
            return domainValues;
        }

        private void Method11Source_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //*** fix to change selected item and values
            //if (this._skipDomainSelChanged)
            //    return;
            ComboBox cb = sender as ComboBox;
            if(cb != null)
            { 
                if (cb.SelectedIndex == -1)
                    return;
                else
                {
                    DataGrid grid = this.Method11Grid as DataGrid;
                    if (grid == null || grid.SelectedIndex == -1)
                        return;

                    //object values = grid.Items[grid.SelectedIndex];
                    DomainMapRow row = grid.Items.GetItemAt(grid.SelectedIndex) as DomainMapRow;
                    DomainMapRow rowSource = grid.Items.GetItemAt(cb.SelectedIndex) as DomainMapRow;
                    if (row!=null)
                    {
                        row.SourceSelectedItem = cb.SelectedIndex;
                        row.SourceTooltip = rowSource.SourceTooltip;
                        cb.ToolTip = rowSource.SourceTooltip;
                    }
                }
            }
        }

        private void Method11Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        private void Method11Target_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null)
            {
                DataGrid grid = this.Method11Grid as DataGrid;
                if (grid == null || grid.SelectedIndex == -1)
                    return;

                DomainMapRow row = grid.Items.GetItemAt(grid.SelectedIndex) as DomainMapRow;
                if (row != null)
                {
                    row.Target = tb.Text;
                    if (tb.Text != "")
                    {
                        row.TargetTooltip = tb.Text;
                        tb.ToolTip = tb.Text;
                        for (int s = 0; s < row.Source.Count; s++)
                        {
                            string dvalue = row.Source[s].Id;
                            if (tb.Text.Equals(dvalue))
                            {
                                tb.ToolTip = row.Source[s].Tooltip;
                                row.TargetTooltip = row.Source[s].Tooltip;
                            }
                        }

                    }
                }
            }
        }
        private void Method11TextChanged(object sender, string sourcetarget)
        {
            //TextBox txt = sender as TextBox;

            //if (Method11Grid.SelectedIndex == -1)
            //    return;

            //if (txt != null)
            //{
            //    for (int i = 0; i < Method11Grid.Items.Count; i++)
            //    {
            //        if (i == Method11Grid.SelectedIndex)
            //        {
            //            object item = Method11Grid.Items.GetItemAt(i);
            //            ValueMapRow row = item as ValueMapRow;
            //            if (row != null)
            //            {
            //                if (sourcetarget == "Source")
            //                    row.Source = txt.Text;
            //                else if (sourcetarget == "Target")
            //                    row.Target = txt.Text;
            //            }
            //        }
            //    }

            //}

        }

    }
}
