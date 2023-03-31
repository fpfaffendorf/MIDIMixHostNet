using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using NAudio.Wave;

namespace MidiMixHostNET
{
    public partial class Main : Form
    {

        private VstPluginContext vstPluginContext;
        private RecordableMixerStream32 Mixer32;
        private AsioOut playbackDevice;

        private VSTStream vstStream;

        public Main()
        {
            InitializeComponent();

        }

        private void Main_Load(object sender, EventArgs e)
        {

            var asioDriverNames = AsioOut.GetDriverNames();
            foreach (string driverName in asioDriverNames)
            {
                listBoxASIO.Items.Add(driverName);
            }
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {

            if (listBoxASIO.SelectedIndex == -1) return;

            vstPluginContext = VstPluginContext.Create(textBoxPath.Text, new DummyHostCommandStub());
            vstPluginContext.PluginCommandStub.Commands.Open();                       

            
            listBoxParameters.Items.Clear();
            for (int i = 0; i < vstPluginContext.PluginInfo.ParameterCount; i++)
            {
                string name = vstPluginContext.PluginCommandStub.Commands.GetParameterName(i);
                string label = vstPluginContext.PluginCommandStub.Commands.GetParameterLabel(i);
                string display = vstPluginContext.PluginCommandStub.Commands.GetParameterDisplay(i);
//                vstPluginContext.PluginCommandStub.Commands.SetParameter(i, -1.00f);
                string displayNew = vstPluginContext.PluginCommandStub.Commands.GetParameterDisplay(i);
                listBoxParameters.Items.Add(i.ToString() + " | " + name + " | " + label + " | " + display + " | " + displayNew);
            }
            
            //MessageBox.Show(vstPluginContext.PluginCommandStub.Commands.GetEffectName());
            //MessageBox.Show(vstPluginContext.PluginCommandStub.Commands.GetProgramName());
            //VstMidiProgramName n = new VstMidiProgramName();

            Mixer32 = new RecordableMixerStream32();
            Mixer32.AutoStop = false;

            playbackDevice = new AsioOut(listBoxASIO.Text);
            playbackDevice.Init(Mixer32);
            playbackDevice.Play();

            vstStream = new VSTStream();
            //vstStream.ProcessCalled += GeneralVST.Stream_ProcessCalled;
            vstStream.pluginContext = vstPluginContext;
            vstStream.SetWaveFormat(44100, 2);

            Mixer32.AddInputStream(vstStream);

            //vstStream.InputWave = @"C:\Users\yo\Music\2018-May-19-18-41-34.wav";

            

            if ((vstPluginContext.PluginInfo.Flags & VstPluginFlags.CanReplacing) == 0)
            {
                MessageBox.Show(this, "This plugin does not process any audio.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            //            c = vstPluginContext.PluginCommandStub.Commands.GetChunk(true);
            //            File.WriteAllBytes("test.txt", c);            

            labelStatus.Text = "Ready !";

        }

        private void GenerateNoiseBtn_Click(object sender, EventArgs e)
        {
            // plugin does not support processing audio
            if ((vstPluginContext.PluginInfo.Flags & VstPluginFlags.CanReplacing) == 0)
            {
                MessageBox.Show(this, "This plugin does not process any audio.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            int inputCount = vstPluginContext.PluginInfo.AudioInputCount;
            //int outputCount = vstPluginContext.PluginInfo.AudioOutputCount;
            int blockSize = 1024;

            // wrap these in using statements to automatically call Dispose and cleanup the unmanaged memory.
            // using var inputMgr = new VstAudioBufferManager(inputCount, blockSize);
            //using var outputMgr = new VstAudioBufferManager(outputCount, blockSize);

            var rnd = new Random((int)DateTime.Now.Ticks);

            for (int i = 0; i < 128; i++)
            {
                // generate a value between -1.0 and 1.0
                //buffer[i] = (float)((rnd.NextDouble() * 2.0) - 1.0);
                //vstStream.inputBuffers[0][i] = (float)((rnd.NextDouble() * 2.0) - 1.0);
                //vstStream.inputBuffers[1][i] = (float)((rnd.NextDouble() * 2.0) - 1.0);
            }



            //vstPluginContext.PluginCommandStub.Commands.SetBlockSize(blockSize);
            //vstPluginContext.PluginCommandStub.Commands.SetSampleRate(44100f);

            // VstAudioBuffer[] inputBuffers = inputMgr.Buffers.ToArray();
            //VstAudioBuffer[] outputBuffers = outputMgr.Buffers.ToArray();

            /*
            vstPluginContext.PluginCommandStub.Commands.MainsChanged(true);
            vstPluginContext.PluginCommandStub.Commands.StartProcess();
            vstPluginContext.PluginCommandStub.Commands.ProcessReplacing(inputBuffers, outputBuffers);
            vstPluginContext.PluginCommandStub.Commands.StopProcess();
            vstPluginContext.PluginCommandStub.Commands.MainsChanged(false);

            for (int i = 0; i < inputBuffers.Length && i < outputBuffers.Length; i++)
            {
                for (int j = 0; j < blockSize; j++)
                {
                    if (inputBuffers[i][j] != outputBuffers[i][j])
                    {
                        if (outputBuffers[i][j] != 0.0)
                        {
                            MessageBox.Show(this, "The plugin has processed the audio.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                    }
                }
            }

            MessageBox.Show(this, "The plugin has passed the audio unchanged to its outputs.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            */
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            vstPluginContext?.Dispose();
            playbackDevice?.Stop();
            playbackDevice?.Dispose();
            Mixer32?.Dispose();
        }

        byte[] c = null;

        private void button1_MouseDown(object sender, MouseEventArgs e)
        {

            var dlg = new EditorFrame
            {
                PluginCommandStub = vstPluginContext.PluginCommandStub,
                HostCommandStub = (DummyHostCommandStub)vstPluginContext.HostCommandStub
            };

            dlg.ShowDialog(this);

            /*

            if (vstPluginContext.PluginCommandStub.Commands.CanDo(
                VstCanDoHelper.ToString(VstPluginCanDo.ReceiveVstMidiEvent)) != VstCanDoResult.Yes)
            {
                MessageBox.Show(this, "This plugin does not process any midi.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);             
            }

            var data1 = new byte[] { 0x90, 60, 100 };
            var event1 = new VstMidiEvent(0, 20, 0, data1, 0, 0);
            var data2 = new byte[] { 0x90, 65, 100 };
            var event2 = new VstMidiEvent(0, 20, 0, data2, 0, 0);
            var events = new VstMidiEvent[] { event1, event2 };

            vstPluginContext.PluginCommandStub.Commands.MainsChanged(true);
            vstPluginContext.PluginCommandStub.Commands.StartProcess();

            vstPluginContext.PluginCommandStub.Commands.ProcessEvents(events);

            vstPluginContext.PluginCommandStub.Commands.StopProcess();
            vstPluginContext.PluginCommandStub.Commands.MainsChanged(false);

            vstPluginContext.PluginCommandStub.Commands.SetChunk(c, true);
            MessageBox.Show("Listo");

            */

        }

        private void button1_MouseUp(object sender, MouseEventArgs e)
        {

        }
    }

}