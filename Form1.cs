using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace ColorImageFromArray
{

    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, ref Rectangle lpRect);
        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr hWnd, ref Rectangle lpRect);

        private int width = 640;
        private int height = 480;
        private int[] pixelData;
        private Bitmap? bitmap;
#if USE_CACHEDBITMAP
        private CachedBitmap? cachedBitmap;
#endif
        private Stopwatch? stopwatch;

        // �}���`�X���b�h�쓮
        private CancellationTokenSource? cts;
        private Task? task;

        private bool isRunning = false;

        private Random random = new Random();

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            pixelData = new int[width * height];
        }

        private Size CalculateFormSize(Size clientSize)
        {
            // �ꎞ�I�ȃt�H�[�����쐬���Ęg�̃T�C�Y���擾����
            using (Form tempForm = new Form())
            {
                tempForm.FormBorderStyle = this.FormBorderStyle;
                tempForm.WindowState = this.WindowState;

                // ���݂̘g�̃T�C�Y���擾����
                Size borderSize = new Size(
                    tempForm.Width - tempForm.ClientSize.Width,
                    tempForm.Height - tempForm.ClientSize.Height
                );

                // �N���C�A���g�T�C�Y�ɘg�̃T�C�Y��������
                return new Size(
                    clientSize.Width + borderSize.Width,
                    clientSize.Height + borderSize.Height
                );
            }
        }

        /// <summary>
        /// �t�H�[�������[�h���ꂽ�Ƃ�
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            // �{�[�_�[�T�C�Y���l�����ăN���C�A���g�T�C�Y��ݒ�
            // �N���C�A���g�̈�̃T�C�Y��ݒ肷��i��F640x480�j
            Size desiredClientSize = new Size(640, 480);

            // �t�H�[���̑S�̃T�C�Y���v�Z����
            Size formSize = CalculateFormSize(desiredClientSize);

            // �t�H�[���̃T�C�Y��ݒ肷��
            this.MaximumSize = this.MinimumSize = formSize;
            this.Size = formSize;

            // �r�b�g�}�b�v���쐬���A���l�z����R�s�[
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

            // Stopwatch�N���X����
            stopwatch = new Stopwatch();
            stopwatch.Start();

            // �^�C�}�[�ݒ�
            SetupTimer();
        }

        /// <summary>
        /// �񓯊��Ńt���[���X�V�^�C�}�[��ݒ�
        /// </summary>
        private void SetupTimer()
        {
            cts = new CancellationTokenSource();

            isRunning = true;
            // �^�X�N����
            task = Task.Run(async () =>
            {
                while (isRunning)
                {
                    // �^�X�N���L�����Z������Ă��Ȃ����`�F�b�N
                    if (cts.Token.IsCancellationRequested)
                    {
                        Debug.WriteLine("Task is cancelled.");
                        break;
                    }
                    await Task.Delay(0);
                    // ������ 17ms ���ƂɎ��s�������R�[�h���L�q���܂�
                    var ts = stopwatch?.Elapsed;
                    if (ts?.Milliseconds >= 17)
                    {
                        float delta_t = (float)ts?.Milliseconds / 17.0f;
                        Debug.WriteLine("RunningLoop " + ts?.Milliseconds + " delta_t " + delta_t);
                        stopwatch?.Restart();
                        UpdateFrame(delta_t); // �t���[���X�V
                        this.Invalidate(); // �ĕ`��
                    }
                }
            }, cts.Token);
        }

        /// <summary>
        /// �\��
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (!isRunning) return;

            // �r�b�g�}�b�v��\��
#if USE_CACHEDBITMAP
            if (cachedBitmap != null)
            {
                g.DrawCachedBitmap(cachedBitmap, 0, 0);
            }
#else
            if (bitmap != null)
            {
                g.DrawImage(bitmap, 0, 0);
            }
#endif
        }

        /// <summary>
        /// �t���[���X�V����
        /// </summary>
        /// <param name="delta_t">����(1.0=�ʏ�j</param>
        private void UpdateFrame(float delta_t)
        {
            if (bitmap == null) return;

            // ���l�z���RGB�f�[�^��ݒ�i�����_���j
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // pixelData[y * width + x] = (255 << 24) | (r << 16) | (g << 8) | b;
                    int pixel = random.Next();
                    pixelData[y * width + x] = (255 << 24) | pixel;
                }
            }
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData? bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            Marshal.Copy(pixelData, 0, bmpData.Scan0, pixelData.Length);
            bitmap.UnlockBits(bmpData);
#if USE_CACHEDBITMAP
            // CachedBitmap���X�V
            using (Graphics g = this.CreateGraphics())
            {
                cachedBitmap = new CachedBitmap(bitmap, g);
            }
#endif
            bmpData = null;
            GC.Collect();
        }

        /// <summary>
        /// �t�H�[�������
        /// </summary>
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //
            cts?.Cancel();
            isRunning = false;

            // �^�X�N���I������܂őҋ@
            task?.Wait();
            task?.Dispose();
            task = null;
            stopwatch?.Stop();
            stopwatch = null;

            // �L�����Z���g�[�N�������
            cts?.Dispose();
            cts = null;

            // �A�C�h����ԃC�x���g����
            cts?.Dispose();
            cts = null;

#if USE_CACHEDBITMAP
            // cachedBitmap���
            cachedBitmap?.Dispose();
            cachedBitmap = null;
#endif
            // bitmap���
            bitmap?.Dispose();
            bitmap = null;

            GC.Collect();
        }

    }
}
