﻿using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Wasapi;
using WWMath;
using WWUtil;
using System.Collections.Generic;

namespace WWImpulseResponse {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();
        }

        private static string AssemblyVersion {
            get { return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        private enum State {
            Init,
            Syncing,
            Running,
            RecCompleted,
        };

        private WasapiCS mWasapiPlay;
        private WasapiCS mWasapiRec;

        private BackgroundWorker mPlayWorker;
        private BackgroundWorker mRecWorker;

        private Wasapi.WasapiCS.CaptureCallback mCaptureDataArrivedDelegate;

        private int mPlayDeviceIdx = -1;
        private int mRecDeviceIdx = -1;
        private int ZERO_FLUSH_MILLISEC = 1000;
        private int TIME_PERIOD = 10000;
        
        private LevelMeter mLevelMeter;

        private State mState = State.Init;

        private PcmDataLib.PcmData mPcmPlay;

        private Random mRand = new Random();

        private LargeArray<byte> mCapturedPcmData;
        private long mReceivedBytes;
        private long mCapturedBytes;

        private Object mLock = new Object();

        private Wasapi.WasapiCS.StateChangedCallback mStateChanged;
        private Preference mPref = null;
        
        private WWRadix2Fft mFFT;
        private int mCaptureCounter = 0;
        private bool mInitialized = false;

        private class StartTestingResult {
            public bool result;
            public string text;
        };

        private BackgroundWorker mBwStartTesting;

        private enum ButtonStartStopState {
            Disable,
            StartEnable,
            StopEnable,
        };

        private void LocalizeUI() {
            mLevelMeterUC.UpdateUITexts();
        }

        private readonly int [] MLS_ORDERS = new int[] {
            16,
            17,
            18,
            19,
            20
        };
        
        private readonly int[] NUM_CH = new int[] {
            2,
            6,
            8
        };

        private List<string> mPlayDeviceIdStringList = new List<string>();
        private List<string> mRecDeviceIdStringList = new List<string>();

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            mPref = PreferenceStore.Load(); 
            LocalizeUI();

            mWasapiPlay = new WasapiCS();
            mWasapiPlay.Init();

            mWasapiRec = new WasapiCS();
            mWasapiRec.Init();
            mCaptureDataArrivedDelegate = new Wasapi.WasapiCS.CaptureCallback(CaptureDataArrived);
            mWasapiRec.RegisterCaptureCallback(mCaptureDataArrivedDelegate);

            mBwStartTesting = new BackgroundWorker();
            mBwStartTesting.DoWork += new DoWorkEventHandler(BwStartTesting_DoWork);
            mBwStartTesting.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BwStartTesting_RunWorkerCompleted);

            mPlayWorker = new BackgroundWorker();
            mPlayWorker.DoWork += new DoWorkEventHandler(PlayDoWork);
            mPlayWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(PlayRunWorkerCompleted);
            mPlayWorker.WorkerSupportsCancellation = true;

            mRecWorker = new BackgroundWorker();
            mRecWorker.DoWork += new DoWorkEventHandler(RecDoWork);
            mRecWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(RecRunWorkerCompleted);
            mRecWorker.WorkerSupportsCancellation = true;
            mRecWorker.WorkerReportsProgress = true;
            mRecWorker.ProgressChanged += new ProgressChangedEventHandler(RecWorkerProgressChanged);

            UpdateDeviceList();

            textBoxLog.Text = string.Format("WWImpulseResponse version {0}\r\n", AssemblyVersion);

            mStateChanged = new Wasapi.WasapiCS.StateChangedCallback(StateChangedCallback);
            mWasapiPlay.RegisterStateChangedCallback(mStateChanged);

            mTimeDomainPlot.SetFunctionType(WWUserControls.TimeDomainPlot.FunctionType.DiscreteTimeSequence);
            mTimeDomainPlot.SetDiscreteTimeSequence(new double[1], 44100);

            mFreqResponse.Mode = WWUserControls.FrequencyResponse.ModeType.ZPlane;
            mFreqResponse.SamplingFrequency = 48000;
            mFreqResponse.PhaseUnwarp = true;
            //mFreqResponse.ShowPhase = false;
            mFreqResponse.ShowGroupDelay = false;
            mFreqResponse.UpdateMagnitudeRange(WWUserControls.FrequencyResponse.MagnitudeRangeType.M48dB);
            mFreqResponse.Update();

            mLevelMeterUC.PreferenceToUI(1, -6, -100);
            mLevelMeterUC.YellowLevelChangeEnable(false);
            mLevelMeterUC.SetParamChangedCallback(LevelMeterUCParamChanged);

            PreferenceToUI();

            mInitialized = true;
        }
        
        /// <summary>
        /// LevelMeterユーザーコントロールの設定がユーザー操作によって変更されたとき呼び出される。
        /// </summary>
        private void LevelMeterUCParamChanged(
                int peakHoldSeconds, int yellowLevelDb, int releaseTimeDbPerSec, bool meterReset) {
            mPref.PeakHoldSeconds = peakHoldSeconds;
            mPref.YellowLevelDb = yellowLevelDb;
            mPref.ReleaseTimeDbPerSec = releaseTimeDbPerSec;

            lock (mLock) {
                mLevelMeter = new LevelMeter(mPref.RecSampleFormat, mPref.NumOfChannels, mPref.PeakHoldSeconds,
                    mPref.RecWasapiBufferSizeMS * 0.001, mPref.ReleaseTimeDbPerSec);
            }
        }

        public void StateChangedCallback(StringBuilder idStr, int dwNewState) {
            Dispatcher.BeginInvoke(new Action(delegate() {
                lock (mLock) {
                    if (mState == State.Init) {
                        StopUnsetup();
                        UpdateDeviceList(); //< この中でbuttonStart.IsEnabledの状態が適切に更新される
                    } else {
                        var playDevice = listBoxPlayDevices.SelectedItem as string;
                        if (playDevice.Equals(idStr.ToString())) {
                            Term();
                            MessageBox.Show(string.Format(Properties.Resources.msgPlayDeviceStateChanged, playDevice));
                            Close();
                        }

                        var recDevice = listBoxRecDevices.SelectedItem as string;
                        if (recDevice.Equals(idStr.ToString())) {
                            Term();
                            MessageBox.Show(string.Format(Properties.Resources.msgRecDeviceStateChanged, recDevice));
                            Close();
                        }
                    }
                }
            }));
        }

        private void UpdateDeviceList() {
            {
                mWasapiPlay.EnumerateDevices(WasapiCS.DeviceType.Play);
                string prevDevice = string.Empty;
                if (0 <= listBoxPlayDevices.SelectedIndex) {
                    prevDevice = listBoxPlayDevices.SelectedItem as string;
                }

                mPlayDeviceIdStringList.Clear();
                listBoxPlayDevices.Items.Clear();
                for (int i=0; i < mWasapiPlay.GetDeviceCount(); ++i) {
                    var attr = mWasapiPlay.GetDeviceAttributes(i);
                    listBoxPlayDevices.Items.Add(attr.Name);
                    mPlayDeviceIdStringList.Add(attr.DeviceIdString);
                    if (attr.Name.Equals(prevDevice)) {
                        listBoxPlayDevices.SelectedIndex = i;
                    }
                }

                if (listBoxPlayDevices.SelectedIndex < 0 && 0 < listBoxPlayDevices.Items.Count) {
                    listBoxPlayDevices.SelectedIndex = 0;
                }
            }

            {
                mWasapiRec.EnumerateDevices(WasapiCS.DeviceType.Rec);
                string prevDevice = string.Empty;
                if (0 <= listBoxRecDevices.SelectedIndex) {
                    prevDevice = listBoxRecDevices.SelectedItem as string;
                }

                mRecDeviceIdStringList.Clear();
                listBoxRecDevices.Items.Clear();
                for (int i=0; i < mWasapiRec.GetDeviceCount(); ++i) {
                    var attr = mWasapiRec.GetDeviceAttributes(i);
                    mRecDeviceIdStringList.Add(attr.DeviceIdString);
                    listBoxRecDevices.Items.Add(attr.Name);
                    if (attr.Name.Equals(prevDevice)) {
                        listBoxRecDevices.SelectedIndex = i;
                    }
                }

                if (listBoxRecDevices.SelectedIndex < 0 && 0 < listBoxRecDevices.Items.Count) {
                    listBoxRecDevices.SelectedIndex = 0;
                }
            }

            UpdateButtonStartStop(ButtonStartStopState.StartEnable);
        }

        private void Exit() {
            Term();
            Close();
        }

        private void Term() {
            // バックグラウンドスレッドにjoinして、完全に止まるまで待ち合わせするブロッキング版のStopを呼ぶ。
            // そうしないと、バックグラウンドスレッドによって使用中のオブジェクトが
            // この後のUnsetupの呼出によって開放されてしまい問題が起きる。
            StopBlocking();

            if (mWasapiRec != null) {
                mWasapiRec.Unsetup();
                mWasapiRec.Term();
                mWasapiRec = null;
            }

            if (mWasapiPlay != null) {
                mWasapiPlay.Unsetup();
                mWasapiPlay.Term();
                mWasapiPlay = null;
            }
        }

        private void Window_Closed(object sender, EventArgs e) {
            Term();

            // 設定ファイルを書き出す。
            PreferenceStore.Save(mPref);
        }

        private void StopBlocking() {
            if (mRecWorker.IsBusy) {
                mRecWorker.CancelAsync();
            }
            while (mRecWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                System.Threading.Thread.Sleep(100);
            }

            if (mPlayWorker.IsBusy) {
                mPlayWorker.CancelAsync();
            }
            while (mPlayWorker.IsBusy) {
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Background,
                        new System.Threading.ThreadStart(delegate { }));

                System.Threading.Thread.Sleep(100);
            }
        }

        private void PreferenceToUI() {
            switch (mPref.MLSOrder) {
            case 16:
            default:
                comboBoxMLSOrder.SelectedIndex = 0;
                break;
            case 17:
                comboBoxMLSOrder.SelectedIndex = 1;
                break;
            case 18:
                comboBoxMLSOrder.SelectedIndex = 2;
                break;
            case 19:
                comboBoxMLSOrder.SelectedIndex = 3;
                break;
            case 20:
                comboBoxMLSOrder.SelectedIndex = 4;
                break;
            }

            switch (mPref.SampleRate) {
            case 44100:
                radioButton44100.IsChecked = true;
                break;
            case 48000:
            default:
                radioButton48000.IsChecked = true;
                break;
            case 88200:
                radioButton88200.IsChecked = true;
                break;
            case 96000:
                radioButton96000.IsChecked = true;
                break;
            case 176400:
                radioButton176400.IsChecked = true;
                break;
            case 192000:
                radioButton192000.IsChecked = true;
                break;
            }

            switch (mPref.PlaySampleFormat) {
            case WasapiCS.SampleFormatType.Sint16:
            default:
                radioButtonPlayPcm16.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint24:
                radioButtonPlayPcm24.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint32V24:
                radioButtonPlayPcm32v24.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint32:
                radioButtonPlayPcm32v32.IsChecked = true;
                break;
            }

            switch (mPref.RecSampleFormat) {
            case WasapiCS.SampleFormatType.Sint16:
            default:
                radioButtonRecPcm16.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint24:
                radioButtonRecPcm24.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint32V24:
                radioButtonRecPcm32v24.IsChecked = true;
                break;
            case WasapiCS.SampleFormatType.Sint32:
                radioButtonRecPcm32v32.IsChecked = true;
                break;
            }

            switch (mPref.NumOfChannels) {
            case 2:
            default:
                comboBoxNumChannels.SelectedIndex = 0;
                break;
            case 6:
                comboBoxNumChannels.SelectedIndex = 1;
                break;
            case 8:
                comboBoxNumChannels.SelectedIndex = 2;
                break;
            }

            textBoxTestChannel.Text = string.Format("{0}", mPref.TestChannel);

            textBoxPlayBufferSize.Text = string.Format("{0}", mPref.PlayWasapiBufferSizeMS);
            textBoxRecBufferSize.Text = string.Format("{0}", mPref.RecWasapiBufferSizeMS);

            switch (mPref.PlayDataFeedMode) {
            case WasapiCS.DataFeedMode.EventDriven:
            default:
                radioButtonPlayEvent.IsChecked = true;
                break;
            case WasapiCS.DataFeedMode.TimerDriven:
                radioButtonPlayTimer.IsChecked = true;
                break;
            }

            switch (mPref.RecDataFeedMode) {
            case WasapiCS.DataFeedMode.EventDriven:
            default:
                radioButtonRecEvent.IsChecked = true;
                break;
            case WasapiCS.DataFeedMode.TimerDriven:
                radioButtonRecTimer.IsChecked = true;
                break;
            }

            checkBoxRecSetDwChannelMask.IsChecked = mPref.SetDwChannelMask;

            if (0 < mPref.PlayPreferredDeviceIdString.Length) {
                for (int i = 0; i < mPlayDeviceIdStringList.Count; ++i) {
                    string s = mPlayDeviceIdStringList[i];
                    if (0 == String.Compare(s, mPref.PlayPreferredDeviceIdString)) {
                        listBoxPlayDevices.SelectedIndex = i;
                    }
                }
            }

            if (0 < mPref.RecPreferredDeviceIdString.Length) {
                for (int i = 0; i < mRecDeviceIdStringList.Count; ++i) {
                    string s = mRecDeviceIdStringList[i];
                    if (0 == String.Compare(s, mPref.RecPreferredDeviceIdString)) {
                        listBoxRecDevices.SelectedIndex = i;
                    }
                }
            }
        }

        private bool UpdateTestParamsFromUI() {

            mPlayDeviceIdx = listBoxPlayDevices.SelectedIndex;
            mPref.PlayPreferredDeviceIdString = mPlayDeviceIdStringList[mPlayDeviceIdx];

            mRecDeviceIdx = listBoxRecDevices.SelectedIndex;
            mPref.RecPreferredDeviceIdString = mRecDeviceIdStringList[mRecDeviceIdx];

            if (radioButton44100.IsChecked == true) {
                mPref.SampleRate = 44100;
            }
            if (radioButton48000.IsChecked == true) {
                mPref.SampleRate = 48000;
            }
            if (radioButton88200.IsChecked == true) {
                mPref.SampleRate = 88200;
            }
            if (radioButton96000.IsChecked == true) {
                mPref.SampleRate = 96000;
            }
            if (radioButton176400.IsChecked == true) {
                mPref.SampleRate = 176400;
            }
            if (radioButton192000.IsChecked == true) {
                mPref.SampleRate = 192000;
            }

            if (radioButtonPlayPcm16.IsChecked == true) {
                mPref.PlaySampleFormat = WasapiCS.SampleFormatType.Sint16;
            }
            if (radioButtonPlayPcm24.IsChecked == true) {
                mPref.PlaySampleFormat = WasapiCS.SampleFormatType.Sint24;
            }
            if (radioButtonPlayPcm32v24.IsChecked == true) {
                mPref.PlaySampleFormat = WasapiCS.SampleFormatType.Sint32V24;
            }
            if (radioButtonPlayPcm32v32.IsChecked == true) {
                mPref.PlaySampleFormat = WasapiCS.SampleFormatType.Sint32;
            }

            if (radioButtonRecPcm16.IsChecked == true) {
                mPref.RecSampleFormat = WasapiCS.SampleFormatType.Sint16;
            }
            if (radioButtonRecPcm24.IsChecked == true) {
                mPref.RecSampleFormat = WasapiCS.SampleFormatType.Sint24;
            }
            if (radioButtonRecPcm32v24.IsChecked == true) {
                mPref.RecSampleFormat = WasapiCS.SampleFormatType.Sint32V24;
            }
            if (radioButtonRecPcm32v32.IsChecked == true) {
                mPref.RecSampleFormat = WasapiCS.SampleFormatType.Sint32;
            }

            if (radioButtonPlayEvent.IsChecked == true) {
                mPref.PlayDataFeedMode = WasapiCS.DataFeedMode.EventDriven;
            }
            if (radioButtonPlayTimer.IsChecked == true) {
                mPref.PlayDataFeedMode = WasapiCS.DataFeedMode.TimerDriven;
            }

            if (radioButtonRecEvent.IsChecked == true) {
                mPref.RecDataFeedMode = WasapiCS.DataFeedMode.EventDriven;
            }
            if (radioButtonRecTimer.IsChecked == true) {
                mPref.RecDataFeedMode = WasapiCS.DataFeedMode.TimerDriven;
            }

            int playBuffSz;
            if (!Int32.TryParse(textBoxPlayBufferSize.Text, out playBuffSz)) {
                MessageBox.Show(Properties.Resources.msgPlayBufferSizeError);
                return false;
            }
            if (playBuffSz <= 0 || 1000 <= playBuffSz) {
                MessageBox.Show(Properties.Resources.msgPlayBufferSizeTooLarge);
                return false;
            }
            mPref.PlayWasapiBufferSizeMS = playBuffSz;

            int recBuffSz;
            if (!Int32.TryParse(textBoxRecBufferSize.Text, out recBuffSz)) {
                MessageBox.Show(Properties.Resources.msgRecBufferSizeError);
                return false;
            }
            if (recBuffSz <= 0 || 1000 <= recBuffSz) {
                MessageBox.Show(Properties.Resources.msgRecBufferSizeTooLarge);
                return false;
            }
            mPref.RecWasapiBufferSizeMS = recBuffSz;

            mPref.MLSOrder = MLS_ORDERS[comboBoxMLSOrder.SelectedIndex];

            int numCh = NUM_CH[comboBoxNumChannels.SelectedIndex];
            mPref.NumOfChannels = numCh;

            mPref.SetDwChannelMask = checkBoxRecSetDwChannelMask.IsChecked == true;

            int testCh = 0;
            if (!Int32.TryParse(textBoxTestChannel.Text, out testCh) || testCh < 0 || numCh <= testCh) {
                MessageBox.Show("Error: test channel number is out of range");
                return false;
            }
            mPref.TestChannel = testCh;

            return true;
        }

        //=========================================================================================================

        class StartTestingArgs {
            public int order;
            public int numChannels;
            public int testChannel;
            public int playDwChannelMask;
            public int recDwChannelMask;
            public string outputFolder;

            public StartTestingArgs(int aOrder, int numCh, int testCh, int playDwChMask, int recDwChMask, string aOutputFolder) {
                order = aOrder;
                numChannels = numCh;
                testChannel = testCh;
                playDwChannelMask = playDwChMask;
                recDwChannelMask = recDwChMask;
                outputFolder = aOutputFolder;
            }
        };

        private StartTestingArgs mStartTestingArgs;

        // 開始ボタンを押すと以下の順に実行される。
        //                BwStartTesting_DoWork()
        //                └PreparePcmData()
        //                BwStartTesting_RunWorkerCompleted()
        //                     ↓                       ↓
        // mPlayWorker.RunWorkerAsync()         mRecWorker.RunWorkerAsync()
        // PlayDoWork()                         RecDoWork() → CaptureDataArrived()
        //   (リピート再生)                        │           ├CaptureSync()
        //                                        │           └CaptureRunning()
        //                                        └ProcessCapturedData() → RecWorkerProgressChanged()
        //
        //                                      ユーザーがStopボタン押下
        //                                             ↓
        // PlayRunWorkerCompleted()   ←──────────── mWasapiPlay.Stop()
        //                                          mWasapiRec.Stop()
        //                                      RecRunWorkerCompleted()
        private void buttonStart_Click(object sender, RoutedEventArgs e) {
            if (!UpdateTestParamsFromUI()) {
                return;
            }

            //Console.WriteLine("buttonStart_Click()");

            groupBoxPcmDataSettings.IsEnabled = false;
            groupBoxPlayback.IsEnabled = false;
            groupBoxRecording.IsEnabled = false;

            UpdateButtonStartStop(ButtonStartStopState.Disable);

            textBoxLog.Text += "Preparing data.\n";
            textBoxLog.ScrollToEnd();

            int numCh = mPref.NumOfChannels;
            int playDwChMask = WasapiCS.GetTypicalChannelMask(numCh);

            int recDwChMask = 0;
            if (mPref.SetDwChannelMask) {
                recDwChMask = WasapiCS.GetTypicalChannelMask(numCh);
            }

            mLevelMeterUC.UpdateNumOfChannels(mPref.NumOfChannels);

            mTimeDomainPlot.Clear();

            mFreqResponse.SamplingFrequency = mPref.SampleRate;
            mFreqResponse.TransferFunction = (WWComplex z) => { return new WWComplex(1, 0); };
            mFreqResponse.Update();

            Directory.CreateDirectory(textboxOutputFolder.Text);

            int numSamples = 1 << mPref.MLSOrder;

            mFFT = new WWRadix2Fft(numSamples);

            mStartTestingArgs = new StartTestingArgs(mPref.MLSOrder,
                    numCh, mPref.TestChannel, playDwChMask, recDwChMask, textboxOutputFolder.Text);
            mBwStartTesting.RunWorkerAsync(mStartTestingArgs);
        }

        private LargeArray<byte> CreatePcmSamples(double[] mls, WasapiCS.SampleFormatType sft, int numCh, int playCh) {
            int sampleBytes = (WasapiCS.SampleFormatTypeToUseBitsPerSample(sft)/8);
            int frameBytes = sampleBytes * numCh;
            long bytes = (long)mls.Length * frameBytes;
            var r = new LargeArray<byte>(bytes);

            long writePos = 0;
            for (long i = 0; i < mls.Length; ++i) {
                for (int ch=0; ch<numCh; ++ch) {
                    if (ch != playCh) {
                        for (int c = 0; c < sampleBytes; ++c) {
                            r.Set(writePos++, 0);
                        }
                    } else {
                        int v = 0x7fffffff;

                        // -6dBする。
                        v /= 2;

                        if (mls[i] < 0) {
                            v = -v;
                        }

                        uint uV = (uint)v;

                        for (int c = 0; c < sampleBytes; ++c) {
                            byte b = (byte)(uV >> (8 * (4 - sampleBytes + c)));
                            r.Set(writePos++, b);
                        }
                    }
                }
            }
            return r;
        }

        private MLSDeconvolution mMLSDecon;

        private void PreparePcmData(StartTestingArgs args) {
            mMLSDecon = new MLSDeconvolution(args.order);
            var seq = mMLSDecon.MLSSequence();

            var sampleData = CreatePcmSamples(seq, mPref.PlaySampleFormat, args.numChannels, args.testChannel);

            // mPcmPlay : テストデータ。このPCMデータを再生し、インパルス応答特性を調べる。
            mPcmPlay = new PcmDataLib.PcmData();
            mPcmPlay.SetFormat(args.numChannels,
                WasapiCS.SampleFormatTypeToUseBitsPerSample(mPref.PlaySampleFormat),
                WasapiCS.SampleFormatTypeToValidBitsPerSample(mPref.PlaySampleFormat),
                mPref.SampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt, seq.Length);
            mPcmPlay.SetSampleLargeArray(sampleData);

            // 録音データ置き場。Maximum Length Seqneuceのサイズよりも1サンプル多くしておく。
            int recBytesPerSample = WasapiCS.SampleFormatTypeToUseBitsPerSample(mPref.RecSampleFormat) / 8;
            mCapturedPcmData = new LargeArray<byte>((long)recBytesPerSample * args.numChannels * (seq.Length+1));
        }

        private void BwStartTesting_DoWork(object sender, DoWorkEventArgs e) {
            //Console.WriteLine("BwStartTesting_DoWork()");
            var args = e.Argument as StartTestingArgs;
            var r = new StartTestingResult();
            r.result = false;
            r.text = "StartTesting failed!\n";
            e.Result = r;

            PreparePcmData(args);

            System.GC.Collect();

            lock (mLock) {
                int hr = 0;

                // 録音
                mCapturedBytes = 0;
                mReceivedBytes = 0;

                mLevelMeter = new LevelMeter(mPref.RecSampleFormat, args.numChannels, mPref.PeakHoldSeconds,
                    mPref.RecWasapiBufferSizeMS * 0.001, mPref.ReleaseTimeDbPerSec);

                hr = mWasapiRec.Setup(mRecDeviceIdx,
                        WasapiCS.DeviceType.Rec, WasapiCS.StreamType.PCM,
                        mPref.SampleRate, mPref.RecSampleFormat, args.numChannels, args.recDwChannelMask,
                        WasapiCS.MMCSSCallType.Enable, WasapiCS.MMThreadPriorityType.None,
                        WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Exclusive,
                        mPref.RecDataFeedMode, mPref.RecWasapiBufferSizeMS, ZERO_FLUSH_MILLISEC, TIME_PERIOD, true);
                if (hr < 0) {
                    r.result = false;
                    r.text = string.Format(Properties.Resources.msgRecSetupError,
                            mPref.SampleRate, mPref.RecSampleFormat, args.numChannels, mPref.RecDataFeedMode,
                            mPref.RecWasapiBufferSizeMS, WasapiCS.GetErrorMessage(hr)) + "\n";
                    e.Result = r;
                    StopUnsetup();
                    return;
                }

                // 再生

                hr = mWasapiPlay.Setup(mPlayDeviceIdx,
                        WasapiCS.DeviceType.Play, WasapiCS.StreamType.PCM,
                        mPref.SampleRate, mPref.PlaySampleFormat, args.numChannels, args.playDwChannelMask,
                        WasapiCS.MMCSSCallType.Enable, WasapiCS.MMThreadPriorityType.None,
                        WasapiCS.SchedulerTaskType.ProAudio, WasapiCS.ShareMode.Exclusive,
                        mPref.PlayDataFeedMode, mPref.PlayWasapiBufferSizeMS, ZERO_FLUSH_MILLISEC, TIME_PERIOD, true);
                if (hr < 0) {
                    mWasapiPlay.Unsetup();
                    r.result = false;
                    r.text = string.Format(Properties.Resources.msgPlaySetupError,
                            mPref.SampleRate, mPref.PlaySampleFormat, args.numChannels,
                            mPref.PlayDataFeedMode, mPref.PlayWasapiBufferSizeMS) + "\n";
                    e.Result = r;
                    return;
                }

                var ss = mWasapiPlay.GetSessionStatus();

                mWasapiPlay.ClearPlayList();
                mWasapiPlay.AddPlayPcmDataStart();
                mWasapiPlay.AddPlayPcmData(0, mPcmPlay.GetSampleLargeArray());
                mWasapiPlay.AddPlayPcmDataEnd();

                mWasapiPlay.SetPlayRepeat(true);

                var playAttr = mWasapiPlay.GetDeviceAttributes(mPlayDeviceIdx);
                var recAttr = mWasapiRec.GetDeviceAttributes(mRecDeviceIdx);

                r.result = true;
                r.text = string.Format(Properties.Resources.msgTestStarted, mPref.SampleRate,
                        mPcmPlay.NumFrames / mPref.SampleRate, mPcmPlay.NumFrames * 0.001 * 0.001);
                r.text += string.Format(Properties.Resources.msgPlaySettings,
                        mPref.PlaySampleFormat, mPref.PlayWasapiBufferSizeMS,
                        mPref.PlayDataFeedMode, playAttr.Name);
                r.text += string.Format(Properties.Resources.msgRecSettings,
                        mPref.RecSampleFormat, mPref.RecWasapiBufferSizeMS,
                        mPref.RecDataFeedMode, recAttr.Name);
                e.Result = r;
            }
        }

        class RecWorkerArgs {
            public string outputFolder;
            public RecWorkerArgs(string path) {
                outputFolder = path;
            }
        };

        void BwStartTesting_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            //Console.WriteLine("BwStartTesting_RunWorkerCompleted()");
            var r = e.Result as StartTestingResult;

            textBoxLog.Text += r.text;
            textBoxLog.ScrollToEnd();

            if (r.result == false) {
                // 失敗。
                groupBoxPcmDataSettings.IsEnabled = true;
                groupBoxPlayback.IsEnabled = true;
                groupBoxRecording.IsEnabled = true;

                UpdateButtonStartStop(ButtonStartStopState.StartEnable);
                return;
            }

            // 成功。
            UpdateButtonStartStop(ButtonStartStopState.StopEnable);

            System.GC.Collect();
            System.Threading.Thread.Sleep(500);

            int hr = mWasapiPlay.StartPlayback(0);
            mPlayWorker.RunWorkerAsync();

            hr = mWasapiRec.StartRecording();


            var recArgs = new RecWorkerArgs(textboxOutputFolder.Text);

            mRecWorker.RunWorkerAsync(recArgs);

            mState = State.Syncing;
        }

        /// <summary>
        /// 再生中。バックグラウンドスレッド。
        /// </summary>
        private void PlayDoWork(object o, DoWorkEventArgs args) {
            //Console.WriteLine("PlayDoWork started");
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!mWasapiPlay.Run(100)) {
                //Console.WriteLine("PlayDoWork ");
                System.Threading.Thread.Sleep(1);
                if (bw.CancellationPending) {
                    Console.WriteLine("PlayDoWork() CANCELED");
                    mWasapiPlay.Stop();
                    args.Cancel = true;
                }
            }

            // 正常に最後まで再生が終わった場合、ここでStopを呼んで、後始末する。
            // キャンセルの場合は、2回Stopが呼ばれることになるが、問題ない!!!
            mWasapiPlay.Stop();

            // 停止完了後タスクの処理は、ここではなく、PlayRunWorkerCompletedで行う。
        }

        /// <summary>
        /// 再生終了。
        /// </summary>
        private void PlayRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            mWasapiPlay.Unsetup();
        }

        private void RecDoWork(object o, DoWorkEventArgs args) {
            var recArgs = args.Argument as RecWorkerArgs;
            BackgroundWorker bw = (BackgroundWorker)o;

            while (!mWasapiRec.Run(1000)) {
                System.Threading.Thread.Sleep(1);
                if (mState == State.RecCompleted) {
                    ProcessCapturedData(bw, recArgs.outputFolder);
                    mCapturedBytes = 0;
                    mState = State.Running;
                }
                if (bw.CancellationPending) {
                    Console.WriteLine("RecDoWork() CANCELED");
                    mWasapiRec.Stop();
                    args.Cancel = true;
                }
            }

            // 再生停止する。
            mWasapiPlay.Stop();

            lock (mLock) {
                mWasapiRec.Stop();
                mWasapiRec.Unsetup();
            }

        }

        private void ProcessCapturedData(BackgroundWorker bw, string folder) {
            // 録音したデータをrecPcmDataに入れる。
            var recPcmData = new PcmDataLib.PcmData();
            recPcmData.SetFormat(mStartTestingArgs.numChannels,
                WasapiCS.SampleFormatTypeToUseBitsPerSample(mPref.RecSampleFormat),
                WasapiCS.SampleFormatTypeToValidBitsPerSample(mPref.RecSampleFormat),
                mPref.SampleRate,
                PcmDataLib.PcmData.ValueRepresentationType.SInt,
                mPcmPlay.NumFrames + 1); //< 再生したMaximum Length Sequenceのサイズよりも1サンプル多い。
            recPcmData.SetSampleLargeArray(mCapturedPcmData);

            // インパルス応答impulse[]を得る。
            var recorded = recPcmData.GetDoubleArray(mStartTestingArgs.testChannel);
            var decon = mMLSDecon.Deconvolution(recorded.ToArray());
            var impulse = new double[decon.Length];
            for (int i = 0; i < decon.Length; ++i) {
                impulse[i] = 2.0 * decon[decon.Length - 1 - i];
            }

            {
                // ファイルに保存。
                string path = string.Format("{0}/ImpulseResponse{1}_{2}.csv", folder, DateTime.Now.ToString("yyyyMMddHHmmss"), mCaptureCounter);
                OutputImpulseToCsvFile(impulse, mPref.SampleRate, path);
            }

            ++mCaptureCounter;

            lock (mLock) {
                mTimeDomainPlot.SetDiscreteTimeSequence(impulse, mPref.SampleRate);
            }

            // 周波数特性を計算する。
            var fr = CalcFrequencyResponse(impulse);

            {
                // ファイルに保存。
                string path = string.Format("{0}/FrequencyResponse{1}_{2}.csv", folder, DateTime.Now.ToString("yyyyMMddHHmmss"), mCaptureCounter);
                OutputFrequencyResponseToCsvFile(fr, mPref.SampleRate, path);
            }

            lock (mLock) {
                mFreqResponse.TransferFunction = (WWComplex z) => {
                    double θ = Math.Atan2(z.imaginary, z.real);
                    double freq01 = θ / Math.PI;

                    int pos = (int)(freq01 * fr.Length / 2);
                    if (pos < 0) {
                        return WWComplex.Zero();
                    }

                    return fr[pos];
                };
            }

            // この後描画ができるタイミング(RecWorkerProgressChanged())でmTimeDomainPlot.Update()を呼ぶ。
            bw.ReportProgress(10);
        }

        private int FindPeak(double [] sequence) {
            int peak = 0;
            double maxMagnitude = 0.0;

            for (int i = 0; i < sequence.Length; ++i) {
                double mag = Math.Abs(sequence[i]);
                if (maxMagnitude < mag) {
                    peak = i;
                    maxMagnitude = mag;
                }
            }

            return peak;
        }

        private WWComplex [] CalcFrequencyResponse(double[] impulse) {
            int peakPos = FindPeak(impulse);

            // peakPosが先頭になるようなシーケンスimpulseA。
            var impulseA = new WWComplex[impulse.Length];
            for (int i=0; i<impulse.Length - peakPos; ++i) {
                impulseA[i] = new WWComplex(impulse[i+peakPos],0);
            }
            for (int i=0; i<peakPos; ++i) {
                impulseA[impulse.Length - peakPos + i] = new WWComplex(impulse[i],0);
            }

            return mFFT.ForwardFft(impulseA);
        }

        private void OutputImpulseToCsvFile(double[] impulse, int sampleRate, string path) {
            using (var sw = new StreamWriter(File.Open(path, FileMode.Create))) {
                sw.WriteLine("Time, Amplitude");
                for (int i = 0; i < impulse.Length; ++i) {
                    double time = (double)i / sampleRate;
                    double v = impulse[i];
                    sw.WriteLine("{0:R}, {1:R}", time, v);
                }
            }
        }

        private void OutputFrequencyResponseToCsvFile(WWComplex[] fr, int sampleRate, string path) {
            using (var sw = new StreamWriter(File.Open(path, FileMode.Create))) {
                sw.WriteLine("Frequency(Hz), Magnitude, Phase");
                for (int i = 0; i < fr.Length; ++i) {
                    double freq = (double)sampleRate * i / fr.Length;

                    double mag = fr[i].Magnitude();
                    double pha = fr[i].Phase();

                    sw.WriteLine("{0:R}, {1:R}, {2:R}", freq, mag, pha);
                }
            }
        }

        /// <summary>
        /// 取得したインパルス応答を描画する。
        /// </summary>
        private void RecWorkerProgressChanged(object sender, ProgressChangedEventArgs e) {
            lock (mLock) {
                mTimeDomainPlot.Update();
                mFreqResponse.Update();
            }
        }

        private void RecRunWorkerCompleted(object o, RunWorkerCompletedEventArgs args) {
            Console.WriteLine("RecRunWorkerCompleted()");

            mLevelMeterUC.SetParamChangedCallback(null);
            mLevelMeterUC.ResetLevelMeter();

            lock (mLock) {
                // 完了。UIの状態を戻す。
                UpdateButtonStartStop(ButtonStartStopState.StartEnable);

                groupBoxPcmDataSettings.IsEnabled = true;
                groupBoxPlayback.IsEnabled = true;
                groupBoxRecording.IsEnabled = true;

                mState = State.Init;

                // プロット描画を更新。
                mTimeDomainPlot.Update();
            }


            textBoxLog.Text += "Finished\n";
            textBoxLog.ScrollToEnd();

        }

        private void StopUnsetup() {
            StopBlocking();
            mWasapiPlay.Unsetup();
            mWasapiRec.Unsetup();
        }

        private void UpdateButtonStartStop(ButtonStartStopState s) {
            switch (s) {
            case ButtonStartStopState.StartEnable:
                if (0 <= listBoxPlayDevices.SelectedIndex &&
                        0 <= listBoxRecDevices.SelectedIndex) {
                    buttonStart.IsEnabled = true;
                } else {
                    buttonStart.IsEnabled = false;
                }
                buttonStop.IsEnabled = false;
                break;
            case ButtonStartStopState.StopEnable:
                buttonStart.IsEnabled = false;
                buttonStop.IsEnabled = true;
                break;
            case ButtonStartStopState.Disable:
                buttonStart.IsEnabled = false;
                buttonStop.IsEnabled = false;
                break;
            }
        }

        private void AbortTest() {
            StopUnsetup();

            UpdateButtonStartStop(ButtonStartStopState.StartEnable);

            groupBoxPcmDataSettings.IsEnabled = true;
            groupBoxPlayback.IsEnabled = true;
            groupBoxRecording.IsEnabled = true;
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e) {
            AbortTest();
        }

        private void CaptureSync(byte[] data) {
            // 1再生ぶん待つ。
            mReceivedBytes += data.Length;
            if (mCapturedPcmData.LongLength <= mReceivedBytes) {
                
                mState = State.Running;
            }
        }

        private void CaptureRunning(byte[] data) {
            // 届いたPCMデータをmCapturedPcmDataにAppendし、
            // mCapturedBytesを更新する。
            if (mCapturedBytes + data.Length <= mCapturedPcmData.LongLength) {
                mCapturedPcmData.CopyFrom(data, 0, mCapturedBytes, data.Length);
                mCapturedBytes += data.Length;

                long capturedFrames = mCapturedBytes
                    / mStartTestingArgs.numChannels
                    / (WasapiCS.SampleFormatTypeToUseBitsPerSample(mPref.RecSampleFormat) / 8);

                //Console.WriteLine("Captured {0} frames", capturedFrames);
            } else {
                int copyBytes = (int)(mCapturedPcmData.LongLength - mCapturedBytes);

                mCapturedPcmData.CopyFrom(data, 0, mCapturedBytes, copyBytes);
                mCapturedBytes += copyBytes;

                // キャプチャー終了. データの整合性チェックはRecRunWorkerCompletedで行う。
                mState = State.RecCompleted;
            }
        }

        private void UpdateLevelMeter(byte[] pcmData) {
            // このスレッドは描画できないので注意。

            double[] peakDb;
            double[] peakHoldDb;

            lock (mLock) {
                mLevelMeter.Update(pcmData);

                if (mLevelMeter.NumChannels <= 2) {
                    peakDb = new double[2];
                    peakHoldDb = new double[2];

                    for (int ch = 0; ch < 2; ++ch) {
                        peakDb[ch] = mLevelMeter.GetPeakDb(ch);
                        peakHoldDb[ch] = mLevelMeter.GetPeakHoldDb(ch);
                    }
                } else {
                    peakDb = new double[8];
                    peakHoldDb = new double[8];

                    for (int ch = 0; ch < 8; ++ch) {
                        peakDb[ch] = mLevelMeter.GetPeakDb(ch);
                        peakHoldDb[ch] = mLevelMeter.GetPeakHoldDb(ch);
                    }
                }
            }

            Dispatcher.BeginInvoke(new Action(delegate() {
                // 描画スレッドで描画する。
                mLevelMeterUC.UpdateLevelMeter(peakDb, peakHoldDb);
            }));
        }

        private void CaptureDataArrived(byte[] data) {
            UpdateLevelMeter(data);

            lock (mLock) {
                // Console.WriteLine("CaptureDataArrived {0} bytes, {1} frames", data.Length, data.Length / (mPcmTest.BitsPerFrame/8));
                switch (mState) {
                case State.Syncing:
                    CaptureSync(data);
                    break;
                case State.Running:
                    CaptureRunning(data);
                    break;
                default:
                    break;
                }
            }
        }

    }
}
