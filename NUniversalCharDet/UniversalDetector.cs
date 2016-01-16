using System;
using Mozilla.NUniversalCharDet.Prober;

namespace Mozilla.NUniversalCharDet
{
    /// <summary>
    /// Description of UniversalDetector.
    /// </summary>
    public class UniversalDetector
    {
        ////////////////////////////////////////////////////////////////
        // constants
        ////////////////////////////////////////////////////////////////
        public static float SHORTCUT_THRESHOLD = 0.95f;
        public static float MINIMUM_THRESHOLD = 0.20f;


        ////////////////////////////////////////////////////////////////
        // inner types
        ////////////////////////////////////////////////////////////////
        public enum InputState
        {
            PURE_ASCII,
            ESC_ASCII,
            HIGHBYTE
        }


        ////////////////////////////////////////////////////////////////
        // fields
        ////////////////////////////////////////////////////////////////
        private InputState inputState;
        private bool done;
        private bool start;
        private bool gotData;
        private byte lastChar;
        private string detectedCharset;

        private CharsetProber[] probers;
        private CharsetProber escCharsetProber;

        private ICharsetListener listener;


        ////////////////////////////////////////////////////////////////
        // methods
        ////////////////////////////////////////////////////////////////
        /**
         * @param listener a listener object that is notified of
         *         the detected encocoding. Can be null.
         */
        public UniversalDetector(ICharsetListener listener)
        {
            this.listener = listener;
            this.escCharsetProber = null;
            this.probers = new CharsetProber[3];
            for (int i = 0; i < this.probers.Length; ++i)
            {
                this.probers[i] = null;
            }

            Reset();
        }
        /// <summary>
        /// 已经获取到足够数据，无需继续传入
        /// </summary>
        /// <returns></returns>
        public bool IsDone()
        {
            return this.done;
        }
        /// <summary>
        /// 返回检测到的编码。如果检测器不能确定使用了什么编码，返回null。
        /// </summary>
        /// <returns></returns>
        public string GetDetectedCharset()
        {
            return this.detectedCharset;
        }

        public void SetListener(ICharsetListener listener)
        {
            this.listener = listener;
        }

        public ICharsetListener GetListener()
        {
            return this.listener;
        }
        /// <summary>
        /// 传入数据
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void HandleData(byte[] buf, int offset, int length)
        {
            if (this.done)
            {
                return;
            }

            if (length > 0)
            {
                this.gotData = true;
            }

            if (this.start)
            {
                this.start = false;
                if (length > 3)
                {
                    int b1 = buf[offset] & 0xFF;
                    int b2 = buf[offset + 1] & 0xFF;
                    int b3 = buf[offset + 2] & 0xFF;
                    int b4 = buf[offset + 3] & 0xFF;

                    switch (b1)
                    {
                        case 0xEF:
                            if (b2 == 0xBB && b3 == 0xBF)
                            {
                                this.detectedCharset = Constants.CHARSET_UTF_8;
                            }
                            break;
                        case 0xFE:
                            if (b2 == 0xFF && b3 == 0x00 && b4 == 0x00)
                            {
                                this.detectedCharset = Constants.CHARSET_X_ISO_10646_UCS_4_3412;
                            }
                            else if (b2 == 0xFF)
                            {
                                this.detectedCharset = Constants.CHARSET_UTF_16BE;
                            }
                            break;
                        case 0x00:
                            if (b2 == 0x00 && b3 == 0xFE && b4 == 0xFF)
                            {
                                this.detectedCharset = Constants.CHARSET_UTF_32BE;
                            }
                            else if (b2 == 0x00 && b3 == 0xFF && b4 == 0xFE)
                            {
                                this.detectedCharset = Constants.CHARSET_X_ISO_10646_UCS_4_2143;
                            }
                            break;
                        case 0xFF:
                            if (b2 == 0xFE && b3 == 0x00 && b4 == 0x00)
                            {
                                this.detectedCharset = Constants.CHARSET_UTF_32LE;
                            }
                            else if (b2 == 0xFE)
                            {
                                this.detectedCharset = Constants.CHARSET_UTF_16LE;
                            }
                            break;
                    } // swich end

                    if (this.detectedCharset != null)
                    {
                        this.done = true;
                        return;
                    }
                }
            } // if (start) end

            int maxPos = offset + length;
            for (int i = offset; i < maxPos; ++i)
            {
                int c = buf[i] & 0xFF;
                if ((c & 0x80) != 0 && c != 0xA0)
                {
                    if (this.inputState != InputState.HIGHBYTE)
                    {
                        this.inputState = InputState.HIGHBYTE;

                        if (this.escCharsetProber != null)
                        {
                            this.escCharsetProber = null;
                        }

                        if (this.probers[0] == null)
                        {
                            this.probers[0] = new MBCSGroupProber();
                        }
                        if (this.probers[1] == null)
                        {
                            this.probers[1] = new SBCSGroupProber();
                        }
                        if (this.probers[2] == null)
                        {
                            this.probers[2] = new Latin1Prober();
                        }
                    }
                }
                else
                {
                    if (this.inputState == InputState.PURE_ASCII &&
                        (c == 0x1B || (c == 0x7B && this.lastChar == 0x7E)))
                    {
                        this.inputState = InputState.ESC_ASCII;
                    }
                    this.lastChar = buf[i];
                }
            } // for end

            CharsetProber.ProbingState st;
            if (this.inputState == InputState.ESC_ASCII)
            {
                if (this.escCharsetProber == null)
                {
                    this.escCharsetProber = new EscCharsetProber();
                }
                st = this.escCharsetProber.handleData(buf, offset, length);
                if (st == CharsetProber.ProbingState.FOUND_IT)
                {
                    this.done = true;
                    this.detectedCharset = this.escCharsetProber.getCharSetName();
                }
            }
            else if (this.inputState == InputState.HIGHBYTE)
            {
                for (int i = 0; i < this.probers.Length; ++i)
                {
                    st = this.probers[i].handleData(buf, offset, length);
                    if (st == CharsetProber.ProbingState.FOUND_IT)
                    {
                        this.done = true;
                        this.detectedCharset = this.probers[i].getCharSetName();
                        return;
                    }
                }
            }
            else
            { // pure ascii
                // do nothing
            }
        }
        /// <summary>
        /// 数据已完全传入
        /// </summary>
        public void DataEnd()
        {
            if (!this.gotData)
            {
                return;
            }

            if (this.detectedCharset != null)
            {
                this.done = true;
                if (this.listener != null)
                {
                    this.listener.Report(this.detectedCharset);
                }
                return;
            }

            if (this.inputState == InputState.HIGHBYTE)
            {
                float proberConfidence;
                float maxProberConfidence = 0.0f;
                int maxProber = 0;

                for (int i = 0; i < this.probers.Length; ++i)
                {
                    proberConfidence = this.probers[i].getConfidence();
                    if (proberConfidence > maxProberConfidence)
                    {
                        maxProberConfidence = proberConfidence;
                        maxProber = i;
                    }
                }

                if (maxProberConfidence > MINIMUM_THRESHOLD)
                {
                    this.detectedCharset = this.probers[maxProber].getCharSetName();
                    if (this.listener != null)
                    {
                        this.listener.Report(this.detectedCharset);
                    }
                }
            }
            else if (this.inputState == InputState.ESC_ASCII)
            {
                // do nothing
            }
            else
            {
                // do nothing
            }
        }

        public void Reset()
        {
            this.done = false;
            this.start = true;
            this.detectedCharset = null;
            this.gotData = false;
            this.inputState = InputState.PURE_ASCII;
            this.lastChar = 0;

            if (this.escCharsetProber != null)
            {
                this.escCharsetProber.reset();
            }

            for (int i = 0; i < this.probers.Length; ++i)
            {
                if (this.probers[i] != null)
                {
                    this.probers[i].reset();
                }
            }
        }

    }

}
