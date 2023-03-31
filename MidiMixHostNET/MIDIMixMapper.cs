using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Melanchall.DryWetMidi.Multimedia;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;

using Jacobi.Vst.Host.Interop;
using Jacobi.Vst.Core;

using NAudio.Wave;

namespace MidiMixHostNET
{
    public partial class MIDIMixMapper : Form
    {

        // Converts a MIDIMixMapper channel to a MIDIMix button
        byte[] channelToNote = new byte[] { 1, 4, 7, 10, 13, 16, 19, 22, 3, 6 };
        // Converts a MIDIMixMapper volume to a MIDIMix CC
        byte[] volumeToCC = new byte[] { 19, 23, 27, 31, 49, 53, 57, 61, 62 };
        // Converts a MIDIMixMapper knob to a MIDIMix CC
        byte[] knobToCC = new byte[] { 16, 17, 18, 20, 21, 22, 24, 25, 26, 28, 29, 30, 46, 47, 48, 50, 51, 52, 54, 55, 56, 58, 59, 60 };

        // Array of buttons associated to the MIDIMixMapper channel change
        Button[] channelToButton = null;
        // Array of group boxes and labels associated to each MIDIMixMapper volume control
        GroupBox[] volumeToGroupBox = null;
        Label[] volumeToLabel = null;
        // Array of custom group boxes and labels associated to each MIDIMixMapper knob control
        CustomGroupBox[] knobToGroupBox = null;
        Label[] knobToLabel = null;
        // Array of data grid cells associated to each MIDIMixMapper knob control
        DataGridViewCell[] knobToCellSQ11 = null;
        DataGridViewCell[] knobToCellSQ12 = null;

        // The value of each MIDIMixMapper knob for each MIDIMixMapper channel
        string[][] knob = {
            // Analog
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            // Valhalla
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            // Sequencer
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
            new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" }
        };
        // The value of each volume control including the master volume
        string[] volume = new string[] { "0", "0", "0", "0", "0", "0", "0", "0", "0" };

        // Matrix of parameters per each Analog Lab MIDIMixMapper channel
        string[][] parametersAnalogLab = new string[7][];
        // Array of Valhalla parameters
        string[] parametersValhalla = new string[24];
        // Matrix of parameters per each Sequencer MIDIMixMapper channel
        string[][] parametersSequencer = new string[2][];

        // Is the MIDIMix phisical knob aligned with the MIDIMixMapper value for this node ?
        bool[] knobAligned = {
            false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false
        };

        // MIDIMix MIDI input and output
        InputDevice midiMixInputDevice = null;
        OutputDevice midiMixOutputDevice = null;

        // Current MIDIMixMapper channel
        int currentChannel = -1;

        // Analog Lab mixer that mixes all the analog lab instances
        RecordableMixerStream32 mixer32AnalogLab;
        // Valhalla mixer that mixes the unique Valhalla instance
        RecordableMixerStream32 mixer32Valhalla;
        // The ASIO playback device
        AsioOut playbackDevice;

        // All max 8 VST and stream pointers for each channel including Analog Lab and Valhalla instances
        VstPluginContext[] vstPluginContext = new VstPluginContext[8];
        VSTStream[] vstStream = new VSTStream[8];

        // Current sequencer dataGridView cell
        int[] sequencerCurrentCell = { 0, 0, 0, 0, 0, 0, 0 };
        // Current sequencer LFO countdown
        int[] sequencerCountdown = { 0, 0, 0, 0, 0, 0, 0 };
        // Keep the last ON note the sequencer played so it can be properly turned OFF
        byte[] sequencerLastOn = { 0, 0, 0, 0, 0, 0, 0 };

        // Replace the following appearances in a parameter's name
        Dictionary<string, string> parameterNameReplacements = new Dictionary<string, string>()
        {
            { "P1 ", "" },
            { " ", "" },
            { "_", "" },
            { "Filter", "Fltr" }
        };

        // MIDIMixMapper knob to Analog Lab parameter mapping 
        int[] knobToAnalogLabParameters = new int[]
        {
            2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 60
        };
        // Analog Lab volume parameter 
        int analogLabVolumeParameter = 1;

        int[] knobToValhallaParameters = new int[]
        {
            0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12
        };

        // Debug text shown in the debug label
        string debugText = "Hi :)";

        // Recording the output
        bool recording = false;
        int recordingTimeMin = 0;
        int recordingTimeSec = 0;

        public MIDIMixMapper()
        {

            InitializeComponent();

            // Initialize matrix structures
            for(byte i = 0; i < parametersAnalogLab.Count(); i++)
                parametersAnalogLab[i] = new string[24];
            for (byte i = 0; i < parametersSequencer.Count(); i++)
                parametersSequencer[i] = new string[24];

            // Initialize the sequencer datagridview
            dataGridView1.GridColor = Color.White;
            dataGridView1.Rows.Add("", "", "Note", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0");
            dataGridView1.Rows.Add("", "", "Vel", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127", "127");
            for (int i = 0; i <= 6; i++)
            {
                dataGridView1.Rows.Add((i + 1).ToString(), "0", "0", "⬤");
                for (int j = 0; j <= 18; j++)
                {
                    dataGridView1.Columns[j].SortMode = DataGridViewColumnSortMode.NotSortable;
                    if (j > 2) dataGridView1.Rows[i + 2].Cells[j].Style.BackColor = Color.DimGray;
                }
            }

            // Fill all pointer arrays
            channelToButton = new Button[] { button1, button2, button3, button4, button5, button6, button7, button8, button29, button37 };
            volumeToGroupBox = new GroupBox[] { groupBox25, groupBox26, groupBox27, groupBox28, groupBox29, groupBox30, groupBox31, groupBox32, groupBox33 };
            volumeToLabel = new Label[] { label25, label26, label27, label28, label29, label30, label31, label32, label33 };
            knobToGroupBox = new CustomGroupBox[] { groupBox1, groupBox2, groupBox3, groupBox4, groupBox5, groupBox6, groupBox7, groupBox8, groupBox9, groupBox10, groupBox11, groupBox12, groupBox13, groupBox14, groupBox15, groupBox16, groupBox17, groupBox18, groupBox19, groupBox20, groupBox21, groupBox22, groupBox23, groupBox24 };
            knobToLabel = new Label[] { label1, label2, label3, label4, label5, label6, label7, label8, label9, label10, label11, label12, label13, label14, label15, label16, label17, label18, label19, label20, label21, label22, label23, label24 };

            knobToCellSQ11 = new DataGridViewCell[] {
                dataGridView1.Rows[0].Cells[3], dataGridView1.Rows[0].Cells[11], dataGridView1.Rows[2].Cells[1],
                dataGridView1.Rows[0].Cells[4], dataGridView1.Rows[0].Cells[12], dataGridView1.Rows[3].Cells[1],
                dataGridView1.Rows[0].Cells[5], dataGridView1.Rows[0].Cells[13], dataGridView1.Rows[4].Cells[1],
                dataGridView1.Rows[0].Cells[6], dataGridView1.Rows[0].Cells[14], dataGridView1.Rows[5].Cells[1],
                dataGridView1.Rows[0].Cells[7], dataGridView1.Rows[0].Cells[15], dataGridView1.Rows[6].Cells[1],
                dataGridView1.Rows[0].Cells[8], dataGridView1.Rows[0].Cells[16], dataGridView1.Rows[7].Cells[1],
                dataGridView1.Rows[0].Cells[9], dataGridView1.Rows[0].Cells[17], dataGridView1.Rows[8].Cells[1],
                dataGridView1.Rows[0].Cells[10], dataGridView1.Rows[0].Cells[18]
            };

            knobToCellSQ12 = new DataGridViewCell[] {
                dataGridView1.Rows[1].Cells[3], dataGridView1.Rows[1].Cells[11], dataGridView1.Rows[2].Cells[2],
                dataGridView1.Rows[1].Cells[4], dataGridView1.Rows[1].Cells[12], dataGridView1.Rows[3].Cells[2],
                dataGridView1.Rows[1].Cells[5], dataGridView1.Rows[1].Cells[13], dataGridView1.Rows[4].Cells[2],
                dataGridView1.Rows[1].Cells[6], dataGridView1.Rows[1].Cells[14], dataGridView1.Rows[5].Cells[2],
                dataGridView1.Rows[1].Cells[7], dataGridView1.Rows[1].Cells[15], dataGridView1.Rows[6].Cells[2],
                dataGridView1.Rows[1].Cells[8], dataGridView1.Rows[1].Cells[16], dataGridView1.Rows[7].Cells[2],
                dataGridView1.Rows[1].Cells[9], dataGridView1.Rows[1].Cells[17], dataGridView1.Rows[8].Cells[2],
                dataGridView1.Rows[1].Cells[10], dataGridView1.Rows[1].Cells[18]
            };

            // All knob texts to empty strings as there's no current channel
            foreach (CustomGroupBox groupBox in knobToGroupBox) groupBox.Text = "";

            // Set the parameters for the sequencer 1
            for (byte i = 0; i < 8; i++)
            {
                parametersSequencer[0][i * 3] = "Note" + (i + 1).ToString();
                parametersSequencer[0][(i * 3) + 1] = "Note" + (i + 9).ToString();
                if (i < 7) parametersSequencer[0][(i * 3) + 2] = "LFO" + (i + 1).ToString();
                else { parametersSequencer[0][23] = "MaxNote"; knob[8][23] = "16"; }
            }
    
            // Set the parameters for the sequencer 2
            for (byte i = 0; i < 8; i++)
            {
                parametersSequencer[1][i * 3] = "Velocity" + (i + 1).ToString();
                knob[9][i * 3] = "127";
                parametersSequencer[1][(i * 3) + 1] = "Velocity" + (i + 9).ToString();
                knob[9][(i * 3) + 1] = "127";
                if (i < 7)
                {
                    parametersSequencer[1][(i * 3) + 2] = "Octave" + (i + 1).ToString();  
                    knob[9][(i * 3) + 2] = "0";
                }
            }

        }

        private void GetParameters(byte channel, int[] knobToParameters, string[] parameters)
        {

            // Get all parameters and configure the MIDIMixMapper knobs
            for (byte i = 0; i < (byte)(knobToParameters.Count()); i++)
            {
                string name = vstPluginContext[channel].PluginCommandStub.Commands.GetParameterName(knobToParameters[i]);
                foreach (var rule in parameterNameReplacements)
                {
                    name = name.Replace(rule.Key, rule.Value);
                }
                int value = (int)(vstPluginContext[channel].PluginCommandStub.Commands.GetParameter(knobToParameters[i]) * (float)127.0);
                knobToGroupBox[i].Text = name;
                knob[channel][i] = value.ToString();
                parameters[i] = name;
            }

            // Clear all other unused parameters
            for (byte i = (byte)(knobToParameters.Count()); i < knobToGroupBox.Count(); i++)
            {
                knobToGroupBox[i].Text = "";
                knob[channel][i] = "0";
                parameters[i] = "";
            }

        }

        // Gets all the Analog Lab parameters for the given channel
        private void GetAnalogLabParameters(byte channel)
        {

            if (channel > 7) return;
            if (vstPluginContext[channel] == null) return;

            GetParameters(channel, knobToAnalogLabParameters, parametersAnalogLab[channel]);

            // Get the volume
            {
                int value = (int)(vstPluginContext[channel].PluginCommandStub.Commands.GetParameter(analogLabVolumeParameter) * (float)127.0);
                volume[channel] = value.ToString();
            }

            // Ask the UI to refresh the knob's text and the volume controller
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

        }

        private void GetValhallaParameters(byte channel)
        {

            if (channel != 7) return;
            if (vstPluginContext[channel] == null) return;

            // Set the AnalogLab mixer as this VST wave stream
            vstStream[channel].WavStream = (WaveStream)mixer32AnalogLab;

            GetParameters(channel, knobToValhallaParameters, parametersValhalla);

            // No volume for this VST
            {
                volumeToGroupBox[channel].Text = "";
                volume[channel] = "0";
            }

            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

        }

        private void UIDrawText()
        {
            labelDebug.Text = DateTime.Now.ToString() + ": " + debugText;
            for (int i = 0; i < volumeToLabel.Count(); i++) volumeToLabel[i].Text = volume[i];
            if (currentChannel != -1) for (int i = 0; i < knobToLabel.Count(); i++) knobToLabel[i].Text = knob[currentChannel][i];
        }

        // Send a MIDI note to the MIDIMix device
        private void SendMIDINoteToMIDIMix(byte note, byte velocity)
        {
            if (note > 127) return;
            if (velocity > 127) return;
            if (midiMixOutputDevice == null) return;
            var midiEvent = new NoteOnEvent();
            midiEvent.Channel = (FourBitNumber)0;
            midiEvent.NoteNumber = (SevenBitNumber)note;
            midiEvent.Velocity = (SevenBitNumber)velocity;
            midiMixOutputDevice.SendEvent(midiEvent);
        }

        // Send a MIDI note to the Analog Lab VST
        private void SendMIDINoteToAnalogLab(byte channel, byte noteOff, byte noteOn, byte velocityOn)
        {

            if (channel > 7) return;
            if (noteOff > 127) return;
            if (noteOn > 127) return;
            if (velocityOn > 127) return;
            if (vstPluginContext[channel] == null) return;

            // Create the Off and On events
            var dataOff = new byte[] { 0x80, noteOff, 0, 0 };
            var eventOff = new VstMidiEvent(0, 0, 0, dataOff, 0, 127, true);
            var dataOn = new byte[] { 0x90, noteOn, velocityOn, 0 };
            var eventOn = new VstMidiEvent(0, 0, 0, dataOn, 0, 127, true);

            // Append so the off/on events go in the right order
            List<VstMidiEvent> events = new List<VstMidiEvent>();
            if (noteOff != 0) events = (events.Append(eventOff).ToList());
            if (noteOn != 0) events = (events.Append(eventOn).ToList());

            // If there are no events there's no need to continue, otherwise send the MIDI events
            if (events.Count == 0) return;
            else
            {
                vstStream[channel]?.Events.AddRange(events);
                debugText = "MIDI " + channel + ", " + noteOff + ", " + noteOn + ", " + velocityOn;
                this.Invoke((MethodInvoker)(() => { UIDrawText(); }));
            }

        }

        // Set a channel as the current channel
        private void ChannelSet(byte channel)
        {

            if (channel > 9) return;

            // Set the channel UI and the MIDIMix buttons as selected
            for (int i = 0; i < 10; i++)
            {
                if (channel == i)
                {
                    SendMIDINoteToMIDIMix(channelToNote[i], 1);
                    channelToButton[i].BackColor = (i <= 7) ? Color.DarkOrange : Color.DarkRed;
                }
                else
                {
                    SendMIDINoteToMIDIMix(channelToNote[i], 0);
                    channelToButton[i].BackColor = Color.Gray;
                }
            }

            // Set the knobs text according to the new channel
            for (int i = 0; i < 24; i++)
            {
                if (channel < 7) knobToGroupBox[i].Text = parametersAnalogLab[channel][i];
                else if (channel == 7) knobToGroupBox[i].Text = parametersValhalla[i];
                else if (channel > 7) knobToGroupBox[i].Text = parametersSequencer[channel - 8][i];
                knobAligned[i] = false;
            }

            // Set this as the new current channel and refresh the MIDIMixMapper application knobs to show the right text and values
            currentChannel = channel;

            // Draw the UI text
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

        }

        // Set the volume value
        private void VolumeSet(byte id, byte volume)
        {

            if (id > 8) return;
            if (volume > 127) return;

            this.volume[id] = volume.ToString();
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

            float f = ((float)volume / (float)127);
            if ((id == 8) || (vstPluginContext[id] == null)) return;
            vstPluginContext[id].PluginCommandStub.Commands.SetParameter(analogLabVolumeParameter, f);
            debugText = "Parameter " + analogLabVolumeParameter + ": " + f.ToString();
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

            volumeToGroupBox[id].BackColor = Color.Blue;
            volumeToGroupBox[id].Tag = DateTime.Now;

        }

        // Set a MIDIMixMapper knob on the current channel to the desired value
        private void KnobSet(byte id, int value)
        {
            if (id > 23) return;
            if (value > 127) return;

            // SQ1.1 Max Note format value (0..16)
            if ((currentChannel == 8) && (id == 23)) value = (int)(value * 0.127);
            // SQ1.2 Octave format value (-10..10)
            if ((currentChannel == 9) && ((id == 2) || (id == 5) || (id == 8) || (id == 11) || (id == 14) || (id == 17) || (id == 20))) value = (int)((value - 64) / 6);

            // Set the current value only if the MIDIMix knob is aligned with the MIDIMixMapper node
            if (knobAligned[id])
            {
                // Refresh the text in the UI
                knob[currentChannel][id] = value.ToString();
                this.Invoke((MethodInvoker)(() => { UIDrawText(); }));
            }
            else
            {
                // Consider the knobs aligned if the MIDIMix knob current value is +/-1 step from the MIDIMixMapper knob
                knobAligned[id] = (value <= int.Parse(knob[currentChannel][id]) + 1) && (value >= int.Parse(knob[currentChannel][id]) - 1);
            }

            // Refresh the background of the UI components
            knobToGroupBox[id].BackColor = Color.Blue;
            knobToGroupBox[id].Tag = DateTime.Now;

            // If knob aligned apply the current value to the VST or any other gadget
            if (knobAligned[id])
            {

                // Set the new value in the VST
                if (currentChannel <= 7) // Analog Lab & Valhalla
                {
                    int[] knobToParameters = knobToAnalogLabParameters;
                    if (currentChannel == 7) knobToParameters = knobToValhallaParameters;
                    float f = ((float)value / (float)127);
                    if (vstPluginContext[currentChannel] == null) return;
                    vstPluginContext[currentChannel].PluginCommandStub.Commands.SetParameter(
                        knobToParameters[id], f);
                    debugText = "Parameter " + knobToParameters[id] + ": " + f.ToString();
                    this.Invoke((MethodInvoker)(() => { UIDrawText(); }));
                }
                else
                if (currentChannel == 8) // SQ1.1
                {
                    if (id < 23)
                    {
                        knobToCellSQ11[id].Value = value.ToString();
                        if ((id == 2) || (id == 5) || (id == 8) || (id == 11) || (id == 14) || (id == 17) || (id == 20)) // If it is an LFO update the sequencer countdown
                            sequencerCountdown[((id + 1) / 3) - 1] = value;
                    }
                }
                else
                if (currentChannel == 9) // SQ1.2
                {
                    if (id < 23)
                        knobToCellSQ12[id].Value = value.ToString();
                }

            }


        }

        // Form load
        private void MIDIMixMapper_Load(object sender, EventArgs e)
        {

            // Fill the MIDI controllers combobox, select the latest one
            for (int i = 0; i < InputDevice.GetDevicesCount(); i++)
                comboBoxController.Items.Add(InputDevice.GetByIndex(i).Name);
            comboBoxController.SelectedIndex = comboBoxController.Items.Count - 1;

            // Fill the ASIO devices combobox, select the latest one
            var asioDriverNames = AsioOut.GetDriverNames();
            foreach (string driverName in asioDriverNames)
                comboBoxASIO.Items.Add(driverName);
            comboBoxASIO.SelectedIndex = comboBoxASIO.Items.Count - 1;

            // Clear the datagrid selection
            dataGridView1.ClearSelection();

            // Draw all pending texts in the UI (mainly the debugLabel)
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

        }

        // ASIO open button click
        private void buttonASIOOpen_Click(object sender, EventArgs e)
        {

            try
            {
                Application.UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;
                mixer32AnalogLab = new RecordableMixerStream32();
                mixer32AnalogLab.AutoStop = false;
                mixer32Valhalla = new RecordableMixerStream32();
                mixer32Valhalla.AutoStop = false;
                playbackDevice = new AsioOut(comboBoxASIO.Text);
                playbackDevice.Init(mixer32Valhalla);
                playbackDevice.Play();
                button11.Visible = true;
                button12.Visible = false;
                comboBoxASIO.Enabled = false;
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening the ASIO device.");
            }
            finally
            {
                Application.UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
            }

        }

        // ASIO close button click
        private void buttonASIOClose_Click(object sender, EventArgs e)
        {
            try
            {
                Application.UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;
                mixer32AnalogLab.Dispose();
                mixer32Valhalla.Dispose();
                playbackDevice.Stop();
                playbackDevice.Dispose();
                mixer32AnalogLab = null;
                mixer32Valhalla = null;
                playbackDevice = null;
                button11.Visible = false;
                button12.Visible = true;
                comboBoxASIO.Enabled = true;
            }
            catch (Exception)
            {
                MessageBox.Show("Error closing the ASIO device.");
            }
            finally
            {
                Application.UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
            }
        }

        // Open the MIDI Mix controller
        private void buttonOpenMIDIMixController_Click(object sender, EventArgs e)
        {
            try
            {
                midiMixInputDevice = InputDevice.GetByName(comboBoxController.Text);
                midiMixOutputDevice = OutputDevice.GetByName(comboBoxController.Text);
                midiMixInputDevice.EventReceived += OnMIDIMixEventReceived;
                midiMixInputDevice.StartEventsListening();
                ChannelSet(0);
                buttonOpenMIDIController.Visible = false;
                buttonCloseMIDIController.Visible = true;
                comboBoxController.Enabled = false;
                timerBackground.Enabled = true;
                buttonREC.Enabled = true;
            }
            catch (Exception)
            {
                MessageBox.Show("Error opening the MIDI device.");
            }
        }

        // Close the MIDI Mix controller
        private void buttonCloseMIDIMixController_Click(object sender, EventArgs e)
        {
            try
            {
                midiMixInputDevice.StopEventsListening();
                midiMixInputDevice.Dispose();
                midiMixOutputDevice.Dispose();
                midiMixInputDevice = null;
                midiMixOutputDevice = null;
                buttonOpenMIDIController.Visible = true;
                buttonCloseMIDIController.Visible = false;
                comboBoxController.Enabled = true;
                timerBackground.Enabled = false;
                buttonREC.Enabled = false;
            }
            catch (Exception)
            {
                MessageBox.Show("Error closing the MIDI device.");
            }
        }

        private void buttonChannel_Click(object sender, EventArgs e)
        {
            if (sender == button1) ChannelSet(0);
            if (sender == button2) ChannelSet(1);
            if (sender == button3) ChannelSet(2);
            if (sender == button4) ChannelSet(3);
            if (sender == button5) ChannelSet(4);
            if (sender == button6) ChannelSet(5);
            if (sender == button7) ChannelSet(6);
            if (sender == button8) ChannelSet(7);
            if (sender == button29) ChannelSet(8);
            if (sender == button37) ChannelSet(9);
        }

        // MIDI Mix event received
        private void OnMIDIMixEventReceived(object sender, MidiEventReceivedEventArgs e)
        {

            // Note on received, a button pressed is assumed.
            if (e.Event is NoteOffEvent)
            {

                // Channel change logic
                if ((((NoteOffEvent)(e.Event)).NoteNumber == 1) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 4) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 7) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 10) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 13) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 16) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 19) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 22) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 3) ||
                    (((NoteOffEvent)(e.Event)).NoteNumber == 6))
                {
                    // Channel Selection
                    for (byte i = 0; i < channelToNote.Count(); i++)
                        if (channelToNote[i] == ((NoteOffEvent)(e.Event)).NoteNumber) ChannelSet(i);
                }

            }

            if (e.Event is ControlChangeEvent)
            {

                // Volume and master volume
                if ((((ControlChangeEvent)(e.Event)).ControlNumber == 19) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 23) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 27) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 31) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 49) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 53) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 57) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 61) ||
                    (((ControlChangeEvent)(e.Event)).ControlNumber == 62))
                {
                    // Volume set
                    for (byte i = 0; i < volumeToCC.Count(); i++)
                        if (volumeToCC[i] == ((ControlChangeEvent)(e.Event)).ControlNumber)
                            VolumeSet(i, ((ControlChangeEvent)(e.Event)).ControlValue);
                }

                // CC Pots
                if (((((ControlChangeEvent)(e.Event)).ControlNumber >= 16) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 18)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 20) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 22)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 24) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 26)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 28) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 30)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 46) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 48)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 50) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 52)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 54) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 56)) ||
                    ((((ControlChangeEvent)(e.Event)).ControlNumber >= 58) && (((ControlChangeEvent)(e.Event)).ControlNumber <= 60)))
                {
                    // Knob set
                    for (byte i = 0; i < knobToCC.Count(); i++)
                        if (knobToCC[i] == ((ControlChangeEvent)(e.Event)).ControlNumber)
                            KnobSet(i, ((ControlChangeEvent)(e.Event)).ControlValue);
                }

            }

        }

        // Button click to instantiate a VST for a given channel
        private void buttonInstantiateVST_Click(object sender, EventArgs e)
        {

            // Quit if there are no mixers, playback or MIDI Mix In/Out devices
            if (mixer32AnalogLab == null) return;
            if (mixer32Valhalla == null) return;
            if (playbackDevice == null) return;
            if (midiMixInputDevice == null) return;
            if (midiMixOutputDevice == null) return;

            // Determine the channel based on the pressed button
            byte channel = 0;
            if (sender == button14) channel = 1;
            if (sender == button15) channel = 2;
            if (sender == button16) channel = 3;
            if (sender == button17) channel = 4;
            if (sender == button18) channel = 5;
            if (sender == button19) channel = 6;
            if (sender == button20) channel = 7;

            // If this channel already has a VST then quit, there's nothing to do here.
            if (vstPluginContext[channel] != null) return;

            try
            {

                Application.UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;

                // In case this is a second button press remove any already instantiated VST and its stream
                // This shouldn't happen as it is controlled by the if/return above
                if (channel == 7) mixer32Valhalla.RemoveInputStream(vstStream[channel]);
                else mixer32AnalogLab.RemoveInputStream(vstStream[channel]);
                vstPluginContext[channel]?.Dispose();
                vstPluginContext[channel] = null;
                vstStream[channel]?.Dispose();
                vstStream[channel] = null;

                // Create an Analog Lab or Valhalla VST depending on the selected channel, open it
                if (channel == 7) vstPluginContext[channel] = VstPluginContext.Create(textBox2.Text, new DummyHostCommandStub());
                else vstPluginContext[channel] = VstPluginContext.Create(textBox1.Text, new DummyHostCommandStub());
                vstPluginContext[channel].PluginCommandStub.Commands.Open();

                // Create the VST stream
                vstStream[channel] = new VSTStream();
                vstStream[channel].pluginContext = vstPluginContext[channel];
                vstStream[channel].SetWaveFormat(44100, 2);

                // Add the input stream to the right mixer
                if (channel == 7) mixer32Valhalla.AddInputStream(vstStream[channel]);
                else mixer32AnalogLab.AddInputStream(vstStream[channel]);

                // Set the button's new text and background color
                ((Button)sender).Text = "CH" + (channel + 1).ToString() + " - " + vstPluginContext[channel].PluginCommandStub.Commands.GetEffectName();
                ((Button)sender).BackColor = Color.PaleTurquoise;

                // Get the VST parameters and prepare the knobs and volume
                if (channel != 7) GetAnalogLabParameters(channel);
                else GetValhallaParameters(channel);

                // Set this channel as the current channel
                ChannelSet(channel);

            }
            catch (Exception)
            {
                MessageBox.Show("Error opening the plugin.");
            }
            finally
            {
                Application.UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
            }

        }

        // Click on a VST edit button
        private void buttonVSTEditor_Click(object sender, EventArgs e)
        {

            byte channel = 0;
            if (sender == button27) channel = 1;
            if (sender == button26) channel = 2;
            if (sender == button25) channel = 3;
            if (sender == button24) channel = 4;
            if (sender == button23) channel = 5;
            if (sender == button22) channel = 6;
            if (sender == button21) channel = 7;

            // No VST, quit
            if (vstPluginContext[channel] == null) return;

            ChannelSet(channel);

            try
            {

                Application.UseWaitCursor = true;
                Cursor.Current = Cursors.WaitCursor;

                if (vstPluginContext[channel] != null)
                {

                    var dlg = new EditorFrame
                    {
                        PluginCommandStub = vstPluginContext[channel].PluginCommandStub,
                        HostCommandStub = (DummyHostCommandStub)vstPluginContext[channel].HostCommandStub
                    };

                    // If this is an Analog Lab VST make sure to get the parameters again as they might have changed 
                    if (channel < 7)
                    {
                        dlg.Tag = channel;
                        dlg.FormClosed += delegate (object? sender, FormClosedEventArgs e) { GetAnalogLabParameters((byte)((Form)sender).Tag); };
                    }
                    dlg.ShowDialog(this);

                }

            }
            catch (Exception)
            {
                MessageBox.Show("Error opening the editor.");
            }
            finally
            {
                Application.UseWaitCursor = false;
                Cursor.Current = Cursors.Default;
            }

        }

        // Analog Lab VST remove
        private void buttonAnalogLabVSTRemove_Click(object sender, EventArgs e)
        {

            Button pluginButton = button13;
            int channel = 0;
            if (sender == button35) { channel = 1; pluginButton = button14; }
            if (sender == button34) { channel = 2; pluginButton = button15; }
            if (sender == button33) { channel = 3; pluginButton = button16; }
            if (sender == button32) { channel = 4; pluginButton = button17; }
            if (sender == button31) { channel = 5; pluginButton = button18; }
            if (sender == button30) { channel = 6; pluginButton = button19; }

            mixer32AnalogLab.RemoveInputStream(vstStream[channel]);

            vstPluginContext[channel]?.Dispose();
            vstPluginContext[channel] = null;
            vstStream[channel]?.Dispose();
            vstStream[channel] = null;

            pluginButton.Text = "CH" + (channel + 1).ToString() + " - ...";
            pluginButton.BackColor = Color.Bisque;

            for (byte i = 0; i < knobToGroupBox.Count(); i++)
            {
                knobToGroupBox[i].Text = "";
                knob[channel][i] = "0";
                parametersAnalogLab[channel][i] = "";
            }
            this.Invoke((MethodInvoker)(() => { UIDrawText(); }));

        }

        private void buttonREC_Click(object sender, EventArgs e)
        {
            recording = !recording;
            if (recording)
            {
                buttonREC.BackColor = Color.LightGreen;
                buttonREC.Text = "Stop";
                // Start recording
                mixer32Valhalla.StreamMixToDisk(textBoxREC.Text);
                mixer32Valhalla.StartStreamingToDisk();
            }
            else
            {
                buttonREC.BackColor = Color.FromArgb(255, 128, 128);
                buttonREC.Text = "REC";
            }
            recordingTimeMin = 0;
            recordingTimeSec = 0;
            groupBoxRecording.Text = "Output Record Path (0:00)";
        }

        private void MIDIMixMapper_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = (MessageBox.Show("Do you really want to quit ?", "Quit", MessageBoxButtons.YesNo) == DialogResult.No);
        }

        private void MIDIMixMapper_FormClosed(object sender, FormClosedEventArgs e)
        {
            timerSeq.Stop();
            midiMixInputDevice?.StopEventsListening();
            midiMixInputDevice?.Dispose();
            midiMixOutputDevice?.Dispose();
            mixer32AnalogLab?.Dispose();
            mixer32Valhalla?.Dispose();
            playbackDevice?.Dispose();
            for (int i = 0; i < vstPluginContext.Count(); i++)
            {
                vstPluginContext[i]?.Dispose();
                vstStream[i]?.Dispose();
            }
        }

        // Sequencer timer
        private void timerSeq_Tick(object sender, EventArgs e)
        {

            for (byte i = 0; i < 7; i++)
            {

                // Check if this channel's LFO is greater than 0
                if (dataGridView1.Rows[i + 2].Cells[1].Value.ToString() != "0")
                {

                    // Decrement the countdown
                    sequencerCountdown[i]--;
                    // If the countdown reached 0 proceed
                    if (sequencerCountdown[i] <= 0)
                    {

                        // Restart the sequencer countdown
                        sequencerCountdown[i] = int.Parse(dataGridView1.Rows[i + 2].Cells[1].Value.ToString());

                        // Get the note off
                        byte noteOff = sequencerLastOn[i];

                        // Move the indicator ahead
                        dataGridView1.Rows[i + 2].Cells[sequencerCurrentCell[i] + 3].Value = "";
                        sequencerCurrentCell[i]++;
                        if (sequencerCurrentCell[i] >= int.Parse(knob[8][23])) sequencerCurrentCell[i] = 0;
                        dataGridView1.Rows[i + 2].Cells[sequencerCurrentCell[i] + 3].Value = "⬤";

                        // Get the note on
                        byte noteOn = byte.Parse(dataGridView1.Rows[0].Cells[sequencerCurrentCell[i] + 3].Value.ToString());
                        byte velocityOn = byte.Parse(dataGridView1.Rows[1].Cells[sequencerCurrentCell[i] + 3].Value.ToString());

                        int noteOnOctaved = noteOn + (int.Parse(dataGridView1.Rows[2+i].Cells[2].Value.ToString()) * 12);
                        if (noteOnOctaved > 127) noteOnOctaved = 127;
                        if (noteOnOctaved < 0) noteOnOctaved = 0;
                        sequencerLastOn[i] = (byte)noteOnOctaved;

                        // Play the MIDI off/on notes
                        SendMIDINoteToAnalogLab(
                            i,
                            noteOff,
                            sequencerLastOn[i],
                            velocityOn
                            );

                    }

                }

            }

        }

        // 1 second timer knob's background refresh
        private void timerBackground_Tick(object sender, EventArgs e)
        {

            for (int i = 0; i < volumeToGroupBox.Count(); i++)
                if ((volumeToGroupBox[i].Tag != null) && (DateTime.Now - ((DateTime)volumeToGroupBox[i].Tag)).TotalSeconds > 1)
                    volumeToGroupBox[i].BackColor = BackColor;
            for (int i = 0; i < knobToGroupBox.Count(); i++)
                if ((knobToGroupBox[i].Tag != null) && (DateTime.Now - ((DateTime)knobToGroupBox[i].Tag)).TotalSeconds > 1)
                    knobToGroupBox[i].BackColor = BackColor;

            // If recording increment the recording time and panel text
            if (recording)
            {
                recordingTimeSec++;
                if (recordingTimeSec >= 60)
                {
                    recordingTimeMin++;
                    recordingTimeSec = 0;
                }
                groupBoxRecording.Text = "Output Record Path ("+ recordingTimeMin.ToString()+":"+ recordingTimeSec.ToString("00") +")";
            }           

        }

    }

}
