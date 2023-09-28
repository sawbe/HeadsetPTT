using HeadsetPTT.Properties;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace HeadsetPTT
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            var items = Enum.GetNames<Xbox360ButtonName>();
            downComboBox.Items.AddRange(items);
            downComboBox.Text = ((Xbox360ButtonName)User.Default.downPtt).ToString();
            upComboBox.Items.AddRange(items);
            upComboBox.Text = ((Xbox360ButtonName)User.Default.upPtt).ToString();
            Icon = Properties.Resources.headset_black;
        }

        public void UpdateConnectionState(DeviceState state, int deviceCount)
        {
            if (connectionLabel.InvokeRequired)
                connectionLabel.Invoke(new Action(() => UpdateStateLabel(state, deviceCount)));
            else
                UpdateStateLabel(state, deviceCount);
        }

        private void UpdateStateLabel(DeviceState state, int deviceCount)
        {
            switch(state)
            {
                case DeviceState.Connected:
                    connectionLabel.Text = string.Format("{0} Device Connected", deviceCount);
                    connectionLabel.ForeColor = Color.Green;
                    break;
                default:
                    connectionLabel.ForeColor = SystemColors.ControlText;
                    connectionLabel.Text = state.ToString();
                    break;
            }
        }

        private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender == downComboBox)
                User.Default.downPtt = (int)Enum.Parse<Xbox360ButtonName>(downComboBox.Text);
            else if (sender == upComboBox)
                User.Default.upPtt = (int)Enum.Parse<Xbox360ButtonName>(upComboBox.Text);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            User.Default.Save();
        }
    }
}