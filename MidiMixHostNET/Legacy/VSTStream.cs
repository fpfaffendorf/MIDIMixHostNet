using System;
using System.Collections;
using Jacobi.Vst.Core;
using Jacobi.Vst.Host.Interop;
using NAudio.Wave;

// Copied from the microDRUM project
// https://github.com/microDRUM
// I think it is created by massimo.bernava@gmail.com
// Modified by perivar@nerseth.com to support processing audio files
namespace MidiMixHostNET
{
	public class VSTStreamEventArgs : EventArgs
	{
		public float MaxL = float.MinValue;
		public float MaxR = float.MaxValue;

		public VSTStreamEventArgs(float maxL, float maxR)
		{
			MaxL = maxL;
			MaxR = maxR;
		}
	}

	public class VSTStream : WaveStream
	{
		public Jacobi.Vst.Host.Interop.VstPluginContext pluginContext = null;
		public event EventHandler<VSTStreamEventArgs> ProcessCalled;

		private int BlockSize = 0;

		VstAudioBuffer[] inputBuffers;
		VstAudioBuffer[] outputBuffers;

		float[] input;
		float[] output;

		private WaveStream wavStream;

		public List<VstMidiEvent> Events = new List<VstMidiEvent>();

		public WaveStream WavStream
        {
			set
            {
				this.wavStream = value;
			}
		}

		public new void Dispose()
		{
			DisposeInputWave();
			base.Dispose();
		}

		private void DisposeInputWave()
		{
			if (wavStream != null)
			{
				this.wavStream.Dispose();
				this.wavStream = null;
			}
		}

		private void RaiseProcessCalled(float maxL, float maxR)
		{
			EventHandler<VSTStreamEventArgs> handler = ProcessCalled;

			if (handler != null)
			{
				handler(this, new VSTStreamEventArgs(maxL, maxR));
			}
		}

		private void UpdateBlockSize(int blockSize)
		{
			BlockSize = blockSize;

			int inputCount = pluginContext.PluginInfo.AudioInputCount;
			int outputCount = pluginContext.PluginInfo.AudioOutputCount;

			var inputMgr = new VstAudioBufferManager(inputCount, blockSize);
			var outputMgr = new VstAudioBufferManager(outputCount, blockSize);

			pluginContext.PluginCommandStub.Commands.SetBlockSize(blockSize);
			pluginContext.PluginCommandStub.Commands.SetSampleRate(WaveFormat.SampleRate);
			pluginContext.PluginCommandStub.Commands.SetProcessPrecision(VstProcessPrecision.Process32);

			inputBuffers = inputMgr.Buffers.ToArray();
			outputBuffers = outputMgr.Buffers.ToArray();

			input = new float[WaveFormat.Channels * blockSize];
			output = new float[WaveFormat.Channels * blockSize];
		}

		private float[] ProcessReplace(int blockSize)
		{
			if (blockSize != BlockSize) UpdateBlockSize(blockSize);

			// check if we are processing a wavestream (VST) or if this is audio outputting only (VSTi)
			if (wavStream != null)
			{
				int sampleCount = blockSize * 2;
				int sampleCountx4 = sampleCount * 4;
				int loopSize = sampleCount / WaveFormat.Channels;

				// Convert byte array into float array and store in Vst Buffers
				// naudio reads an buffer of interlaced float's
				// must take every 4th byte and convert to float
				// Vst.Net audio buffer format (-1 to 1 floats).
				var naudioBuf = new byte[blockSize * WaveFormat.Channels * 4];
				int bytesRead = wavStream.Read(naudioBuf, 0, sampleCountx4);

				// populate the inputbuffers with the incoming wave stream
				// TODO: do not use unsafe - but like this http://vstnet.codeplex.com/discussions/246206 ?
				// this whole section is modelled after http://vstnet.codeplex.com/discussions/228692
				unsafe
				{
					fixed (byte* byteBuf = &naudioBuf[0])
					{
						float* floatBuf = (float*)byteBuf;
						int j = 0;
						for (int i = 0; i < loopSize; i++)
						{
							inputBuffers[0][i] = *(floatBuf + j);
							j++;
							inputBuffers[1][i] = *(floatBuf + j);
							j++;
						}
					}
				}
			}

			try
			{
				//pluginContext.PluginCommandStub.MainsChanged(true);
				pluginContext.PluginCommandStub.Commands.StartProcess();
				pluginContext.PluginCommandStub.Commands.ProcessEvents(Events.ToArray());
				Events.Clear();
				pluginContext.PluginCommandStub.Commands.ProcessReplacing(inputBuffers, outputBuffers);
				pluginContext.PluginCommandStub.Commands.StopProcess();
				//pluginContext.PluginCommandStub.MainsChanged(false);
			}
			catch (Exception ex)
			{
				Console.Out.WriteLine(ex.Message);
			}

			int indexOutput = 0;

			float maxL = float.MinValue;
			float maxR = float.MinValue;

			for (int j = 0; j < BlockSize; j++)
			{
				output[indexOutput] = outputBuffers[0][j];
				output[indexOutput + 1] = outputBuffers[1][j];

				maxL = Math.Max(maxL, output[indexOutput]);
				maxR = Math.Max(maxR, output[indexOutput + 1]);
				indexOutput += 2;
			}
			RaiseProcessCalled(maxL, maxR);
			return output;
		}

		public int Read(float[] buffer, int offset, int sampleCount)
		{
			// CALL VST PROCESS HERE WITH BLOCK SIZE OF sampleCount
			float[] tempBuffer = ProcessReplace(sampleCount / 2);

			// Copying Vst buffer inside Audio buffer, no conversion needed for WaveProvider32
			for (int i = 0; i < sampleCount; i++)
				buffer[i + offset] = tempBuffer[i];

			return sampleCount;
		}


		private WaveFormat waveFormat;

		public void SetWaveFormat(int sampleRate, int channels)
		{
			this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var waveBuffer = new WaveBuffer(buffer);
			int samplesRequired = count / 4;
			int samplesRead = Read(waveBuffer.FloatBuffer, offset / 4, samplesRequired);
			return samplesRead * 4;
		}

		public override WaveFormat WaveFormat
		{
			get { return waveFormat; }
		}


		public override long Length
		{
			get { return long.MaxValue; }
		}

		public override long Position
		{
			get
			{
				return 0;
			}
			set
			{
				long x = value;
			}
		}
	}
}