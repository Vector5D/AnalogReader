using System.Configuration;
using System.Diagnostics;
using OpenCvSharp;
using SkiaSharp;
using System.Text;
using System.Text.Json;
using Point = OpenCvSharp.Point;
using Timer = System.Windows.Forms.Timer;

namespace AnalogReader
{
    public partial class FormMain : Form
    {
        // RTSPカメラ関連 (OpenCVSharp版)
        private VideoCapture? _capture;
        private Timer? _timer;
        private bool _isProcessingFrame = false;
        private string? rtspUrl = null;

        private bool _initFlg = true;
        private bool RTSPShow = false;
        private bool RTSPBeforeShow = false;

        // カメラ設定
        private int cameraFrameWidth = 1280;
        private int cameraFrameHeight = 1024;
        private int cameraFps = 30;

        // 針の情報を格納する構造体
        public class NeedleDetectionResult
        {
            /// <summary>
            /// 針の角度（度）
            /// </summary>
            public double AngleDegrees { get; set; }
            /// <summary>
            /// 電圧値
            /// </summary>
            public double MeterValue { get; set; }
            /// <summary>
            /// 検出信頼度（0-1）
            /// </summary>
            public double Confidence { get; set; }
            /// <summary>
            /// 検出に使用した手法
            /// </summary>
            public string? DetectionMethod { get; set; }
            /// <summary>
            /// 針描画画像一時保管ファイルパス
            /// </summary>
            public string? AnnotatedImagePath { get; set; }
            /// <summary>
            /// Pythonからのエラーメッセージ
            /// </summary>
            public string? ErrorMessage { get; set; }
        }

        private sealed class PythonResult
        {
            public double angle_deg { get; set; }
            public double value { get; set; }
            public double confidence { get; set; }
            public string? annotated_path { get; set; }
            public string? error { get; set; }
        }

        public FormMain()
        {
            InitializeComponent();
            InitializeSetting();
        }

        /// <summary>
        /// 初期設定を読み込み
        /// </summary>
        private void InitializeSetting()
        {
            // 設定値の取得
            rtspUrl = ConfigurationManager.AppSettings["cameraAddress"];
        }

        #region RTSP再生

        /// <summary>
        /// RTSPカメラの初期化 (OpenCVSharp版)
        /// </summary>
        private void InitializeRTSPCamera()
        {
            try
            {
                // カメラの接続を閉じる（既存の接続がある場合）
                _capture?.Dispose();

                if (string.IsNullOrWhiteSpace(rtspUrl))
                {
                    MessageBox.Show("cameraAddress が設定されていません。", "エラー",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "カメラ接続エラー（設定不足）";
                    lblStatus.ForeColor = Color.Red;
                    return;
                }

                // WebカメラまたはRTSPストリームに接続
                if (int.TryParse(rtspUrl, out int cameraIndex))
                {
                    // インデックスが数値の場合はWebカメラとして接続
                    _capture = new VideoCapture(cameraIndex);

                    bool ok = true;

                    if (!int.TryParse(ConfigurationManager.AppSettings["cameraFrameWidth"], out cameraFrameWidth) || cameraFrameWidth <= 0) ok = false;
                    if (!int.TryParse(ConfigurationManager.AppSettings["cameraFrameHeight"], out cameraFrameHeight) || cameraFrameHeight <= 0) ok = false;
                    if (!int.TryParse(ConfigurationManager.AppSettings["cameraFps"], out cameraFps) || cameraFps <= 0) ok = false;

                    if (!ok)
                    {
                        lblStatus.Text = "カメラ設定値の解析に失敗しました。App.config を確認してください。";
                        lblStatus.ForeColor = Color.Red;
                        MessageBox.Show("カメラ設定値に不正があります。App.config を確認してください。", "設定エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return; //中断
                    }

                    // カメラのプロパティを設定
                    _capture.Set(VideoCaptureProperties.FrameWidth, cameraFrameWidth);
                    _capture.Set(VideoCaptureProperties.FrameHeight, cameraFrameHeight);
                    _capture.Set(VideoCaptureProperties.Fps, cameraFps);
                }
                else
                {
                    // URL形式の場合はRTSPストリームとして接続
                    _capture = new VideoCapture(rtspUrl);
                }

                // カメラが正常に開かれたか確認
                if (!_capture.IsOpened())
                {
                    MessageBox.Show("カメラデバイスに接続できません。設定を確認してください。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "カメラ接続エラー";
                    lblStatus.ForeColor = Color.Red;
                    return;
                }

                // タイマーの設定
                int timerInterval = 67; // デフォルト値 67msごとに更新 15fps
                if (!int.TryParse(ConfigurationManager.AppSettings["timerInterval"], out timerInterval) || cameraFrameWidth <= 0)
                {
                    lblStatus.Text = "タイマー設定値の解析に失敗しました。App.config を確認してください。";
                }

                _timer?.Stop();
                _timer?.Dispose();

                _timer = new Timer
                {
                    Interval = timerInterval
                };
                _timer.Tick += UpdateFrame;
                _timer.Start();

                lblStatus.Text = "カメラ接続成功";
                lblStatus.ForeColor = Color.Green;
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"カメラ接続エラー: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"カメラ接続中にエラーが発生しました。\n{ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// カメラのフレーム処理
        /// </summary>
        private void UpdateFrame(object? sender, EventArgs e)
        {
            if (_isProcessingFrame || _capture == null || !_capture.IsOpened())
                return;

            _isProcessingFrame = true;

            try
            {
                // フレームを取得
                using var frame = new Mat();
                if (_capture.Read(frame) && !frame.Empty())
                {
                    // フレームをBitmapに変換してPictureBoxに表示
                    using var frameBitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(frame);

                    pbOriginalImage.Image?.Dispose();
                    pbOriginalImage.Image = new Bitmap(frameBitmap);
                    if (_initFlg)
                    {
                        InputUnLock();
                        RTSPShow = true;
                        RTSPBeforeShow = true;
                        _initFlg = false;
                        btnRTSP.Text = "RTSP動画停止";
                        lblOriginal.Text = "RTSP動画";
                    }
                }
                else
                {
                    // 読み込み失敗またはストリームの終了
                    Debug.WriteLine("フレーム取得エラー: フレームが空または読み込みに失敗しました");

                    // 再接続を試みるかエラー処理を行う
                    if (_capture != null && !_capture.IsOpened())
                    {
                        _timer?.Stop();
                        lblStatus.Text = "カメラ接続が切断されました。再接続を試みます...";
                        lblStatus.ForeColor = Color.Orange;

                        // 1秒後に再接続を試みる
                        System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ =>
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                InitializeRTSPCamera();
                            }));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"フレーム取得エラー: {ex.Message}");
            }
            finally
            {
                _isProcessingFrame = false;
            }
        }

        /// <summary>
        /// RTSPカメラの一時停止（接続は維持）
        /// </summary>
        private void PauseRTSPCamera()
        {
            try
            {
                _timer?.Stop();  // タイマーだけ停止

                lblStatus.Text = "カメラを一時停止しました";
                lblStatus.ForeColor = Color.DarkOrange;
                RTSPShow = false;
                RTSPBeforeShow = true;
                btnRTSP.Text = "RTSP動画再生";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"一時停止中のエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// RTSPカメラの再開（Timerを再始動）
        /// </summary>
        private void ResumeRTSPCamera()
        {
            try
            {
                if (_capture != null && _capture.IsOpened())
                {
                    _timer?.Start();
                    lblStatus.Text = "カメラを再開しました";
                    lblStatus.ForeColor = Color.Green;
                    RTSPShow = true;
                    RTSPBeforeShow = false;
                    btnRTSP.Text = "RTSP動画停止";
                    lblOriginal.Text = "RTSP動画";
                }
                else
                {
                    lblStatus.Text = "カメラが未接続です";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"再開中のエラー: {ex.Message}");
            }
        }

        #endregion

        #region YOLOv8画像解析

        /// <summary>
        /// 画像解析処理
        /// <summary>
        private void ProcessImage(string imagePath)
        {

            try
            {
                lblStatus.Text = "画像を処理しています...";
                lblStatus.ForeColor = Color.Blue;

                // 元画像を表示
                using (var img = Image.FromFile(imagePath))
                {
                    pbOriginalImage.Image?.Dispose();
                    pbOriginalImage.Image = new Bitmap(img);
                }

                // SkiaSharpで画像を読み込み
                using var originalBitmap = SKBitmap.Decode(imagePath);

                try
                {
                    // Tempパスとして C:\temp_ai を優先(Pythonの一部ライブラリが非ASCII文字を含むパスでエラーを出す可能性があるため)
                    string safeTempDir = Path.Combine("C:", "temp_ai");
                    string tempBase;

                    try
                    {
                        if (!Directory.Exists(safeTempDir))
                        {
                            Directory.CreateDirectory(safeTempDir);
                        }
                        tempBase = safeTempDir;
                    }
                    catch
                    {
                        // C:\temp_ai 作成失敗時はシステムのTempを使う
                        tempBase = Path.GetTempPath();
                    }

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string originalImagePath = Path.Combine(tempBase, $"original_{timestamp}.jpg");

                    Debug.WriteLine($"オリジナル画像の保存先: {originalImagePath}");

                    // 保存処理（SKBitmap → JPEGファイル）
                    using (var image = SKImage.FromBitmap(originalBitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 95))
                    {
                        File.WriteAllBytes(originalImagePath, data.ToArray());
                    }

                    // 保存成功チェック
                    if (!File.Exists(originalImagePath))
                        throw new Exception($"オリジナル画像ファイルの作成に失敗: {originalImagePath}");

                    var fileInfo = new FileInfo(originalImagePath);
                    if (fileInfo.Length == 0)
                        throw new Exception("作成された画像ファイルのサイズが0です");

                    Debug.WriteLine($"オリジナル画像の保存成功: サイズ={fileInfo.Length} bytes");

                    // Python針検出呼び出し
                    var needleResult = DetectNeedleWithPython(originalImagePath);

                    if (needleResult != null && needleResult.Confidence > 0)
                    {
                        lblStatus.Text = $"電圧値: {needleResult.MeterValue:F2}V、針角度: {needleResult.AngleDegrees:F1}°、信頼度: {needleResult.Confidence:F2}";
                        lblStatus.ForeColor = Color.DarkGreen;

                        if (!string.IsNullOrEmpty(needleResult.AnnotatedImagePath) &&
                        File.Exists(needleResult.AnnotatedImagePath))
                        {
                            ShowPictureThenDelete(pbCroppedImage, needleResult.AnnotatedImagePath);
                        }


                        Debug.WriteLine($"針検出成功！");
                        Debug.WriteLine($"電圧値: {needleResult.MeterValue:F2}V");
                        Debug.WriteLine($"角度: {needleResult.AngleDegrees:F1}°");
                        Debug.WriteLine($"信頼度: {needleResult.Confidence:F2}");
                    }
                    else
                    {
                        lblStatus.Text = !string.IsNullOrEmpty(needleResult?.ErrorMessage)
                            ? $"針検出失敗: {needleResult.ErrorMessage}"
                            : "針検出に失敗しました。";
                        lblStatus.ForeColor = Color.Red;

                        if (!string.IsNullOrEmpty(needleResult?.AnnotatedImagePath) &&
                        File.Exists(needleResult.AnnotatedImagePath))
                        {
                            ShowPictureThenDelete(pbCroppedImage, needleResult.AnnotatedImagePath);
                        }

                    }

                    // 後処理：一時ファイル削除
                    try
                    {
                        System.Threading.Thread.Sleep(200);
                        if (File.Exists(originalImagePath))
                        {
                            File.Delete(originalImagePath);
                            Debug.WriteLine($"オリジナル画像一時ファイル削除: {originalImagePath}");
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        Debug.WriteLine($"一時ファイル削除エラー: {deleteEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = $"針検出処理中にエラーが発生しました: {ex.Message}";
                    lblStatus.ForeColor = Color.Red;
                    Debug.WriteLine($"針検出中の例外: {ex.Message}");
                }


            }
            catch (Exception ex)
            {
                lblStatus.Text = $"処理エラー: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"画像処理中にエラーが発生しました。\n{ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ShowPictureThenDelete(PictureBox box, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            try
            {
                using (var img = Image.FromFile(path))
                {
                    var bmp = new Bitmap(img);
                    box.Image?.Dispose();
                    box.Image = bmp;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"画像表示エラー: {ex.Message}");
            }
            finally
            {
                try
                {
                    Thread.Sleep(100);
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"デバッグ画像削除エラー: {ex.Message}");
                }
            }
        }

        #endregion

        #region ボタンクリック
        private void btnSelectImage_Click(object sender, EventArgs e)
        {
            InputLock();
            if (RTSPShow)
            {
                PauseRTSPCamera();
            }
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "画像ファイル|*.jpg;*.jpeg;*.png;*.bmp;*.gif";
                openFileDialog.Title = "画像を選択してください";

                var result = openFileDialog.ShowDialog();

                if (result == DialogResult.OK)
                {
                    lblOriginal.Text = "撮影画像";
                    lblStatus.Text = "画像を解析中...";
                    lblStatus.ForeColor = Color.Blue;
                    lblStatus.Refresh();
                    ProcessImage(openFileDialog.FileName);
                }
                else
                {
                    lblStatus.Text = "画像選択がキャンセルされました";
                    lblStatus.ForeColor = Color.Gray;
                    if (RTSPBeforeShow)
                    {
                        if (_initFlg)
                        {
                            InitializeRTSPCamera();
                        }
                        else
                        {
                            ResumeRTSPCamera();
                        }
                    }
                }
            }
            btnSelectImage.Enabled = true;
            btnRTSP.Enabled = true;

        }

        private void btnShot_Click(object sender, EventArgs e)
        {
            InputLock();
            try
            {
                PauseRTSPCamera();
                lblStatus.Text = "画像を撮影後解析中...";
                lblStatus.ForeColor = Color.Blue;
                lblStatus.Refresh();

                // 現在の画像を保存
                if (pbOriginalImage.Image != null)
                {
                    lblOriginal.Text = "撮影画像";
                    using var currentImage = new Bitmap(pbOriginalImage.Image);

                    // 保存先のディレクトリ（例: ドキュメントフォルダ内の "AnalogCaptures"）
                    string saveDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "AnalogCaptures"
                    );

                    // ディレクトリがなければ作成
                    if (!Directory.Exists(saveDir))
                    {
                        Directory.CreateDirectory(saveDir);
                    }

                    // ファイル名の生成（タイムスタンプ付き）
                    string saveFileName = $"captured_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    string savePath = Path.Combine(saveDir, saveFileName);

                    // PNG形式で保存
                    currentImage.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);

                    // 保存成功のメッセージ（任意）
                    Debug.WriteLine($"PNG画像を保存しました: {savePath}");


                    // 一時ファイルに保存（YOLO処理用）
                    string tempFilePath = Path.Combine(Path.GetTempPath(), $"captured_frame_{DateTime.Now.Ticks}.jpg");
                    currentImage.Save(tempFilePath, System.Drawing.Imaging.ImageFormat.Jpeg);

                    // 画像を処理
                    ProcessImage(tempFilePath);

                    // 一時ファイルを削除
                    try { File.Delete(tempFilePath); } catch { /* 無視 */ }
                }
                else
                {
                    lblStatus.Text = "カメラからフレームを取得できませんでした";
                    lblStatus.ForeColor = Color.Red;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"撮影エラー: {ex.Message}";
                lblStatus.ForeColor = Color.Red;
                MessageBox.Show($"画像の撮影中にエラーが発生しました。\n{ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            btnSelectImage.Enabled = true;
            btnRTSP.Enabled = true;

        }

        private void btnRTSP_Click(object sender, EventArgs e)
        {
            InputLock();
            try
            {
                if (RTSPShow)
                {
                    PauseRTSPCamera();
                }
                else
                {
                    if (_initFlg)
                    {
                        lblStatus.Text = "RTSPサーバーに接続中です...";
                        lblStatus.ForeColor = Color.Blue;
                        lblStatus.Refresh();

                        InitializeRTSPCamera();
                    }
                    else
                    {
                        ResumeRTSPCamera();
                    }
                    pbCroppedImage.Image = null;
                }
            }
            finally
            {
                InputUnLock();
            }
        }


        #endregion

        #region python

        /// <summary>
        /// Pythonスクリプトを使用して電圧計の針を検出する
        /// </summary>
        /// <param name="imagePath">処理する画像のパス</param>
        /// <returns>針の検出結果（角度と値）</returns>
        public NeedleDetectionResult DetectNeedleWithPython(string imagePath)
        {
            try
            {
                Debug.WriteLine("=== DetectNeedleWithPython 開始 ===");
                Debug.WriteLine($"入力画像パス: {imagePath}");

                // Pythonスクリプトのパス
                string scriptPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "PythonScript",
                    "main.py");

                if (!File.Exists(scriptPath))
                {
                    Debug.WriteLine($"スクリプトが見つかりません: {scriptPath}");
                    throw new FileNotFoundException($"Pythonスクリプトが見つかりません: {scriptPath}");
                }

                if (!File.Exists(imagePath))
                {
                    Debug.WriteLine($"画像ファイルが見つかりません: {imagePath}");
                    throw new FileNotFoundException($"画像ファイルが見つかりません: {imagePath}");
                }

                var payload = new
                {
                    imagePath = imagePath,
                };
                string jsonArg = JsonSerializer.Serialize(payload);


                // プロセス情報を設定
                var processInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                processInfo.ArgumentList.Add(scriptPath);
                processInfo.ArgumentList.Add(jsonArg);

                string stdout, stderr;
                int exitCode;

                using (var process = Process.Start(processInfo)!)
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                stdout = stdout.Trim().TrimStart('\ufeff');
                int braceStart = stdout.IndexOf('{');
                int braceEnd = stdout.LastIndexOf('}');

                PythonResult? py = null;
                if (braceStart >= 0 && braceEnd >= braceStart)
                {
                    string json = stdout.Substring(braceStart, braceEnd - braceStart + 1);
                    try
                    {
                        py = JsonSerializer.Deserialize<PythonResult>(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("JSONパース失敗: " + ex.Message);
                        Debug.WriteLine("JSON:\n" + json);
                    }
                }
                else
                {
                    Debug.WriteLine("[PY STDOUT RAW]\n" + stdout);
                    Debug.WriteLine("[PY STDERR RAW]\n" + stderr);
                }

                // 画像パス補正
                string scriptDir = Path.GetDirectoryName(scriptPath)!;
                static string Fix(string? p, string baseDir)
                {
                    if (string.IsNullOrWhiteSpace(p)) return p ?? string.Empty;
                    return Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(baseDir, p));
                }

                // py.error があれば それを優先して失敗扱い（マスク/注釈パスも返す）
                if (py != null && !string.IsNullOrEmpty(py.error))
                {
                    return new NeedleDetectionResult
                    {
                        DetectionMethod = "Pythonエラー",
                        Confidence = 0.0,
                        ErrorMessage = string.IsNullOrWhiteSpace(stderr) ? py.error : $"{py.error} | {stderr.Trim()}",
                        //MaskImagePath = Fix(py.mask_path, scriptDir),
                        AnnotatedImagePath = Fix(py.annotated_path, scriptDir)
                    };
                }

                // py が取れていれば成功として返す
                if (py != null)
                {
                    var detectedResult = new NeedleDetectionResult
                    {
                        AngleDegrees = py.angle_deg,
                        MeterValue = py.value,
                        Confidence = py.confidence,
                        DetectionMethod = "Python処理",
                        AnnotatedImagePath = Fix(py.annotated_path, scriptDir)
                    };
                    return detectedResult;
                }

                // ここまでで JSON が取れなかった場合のみ、exitCode を見てエラー化
                if (exitCode != 0)
                {
                    Debug.WriteLine("[PY STDERR]\n" + stderr);
                    Debug.WriteLine("[PY STDOUT]\n" + stdout);
                    return new NeedleDetectionResult
                    {
                        DetectionMethod = $"Python処理失敗 (終了コード:{exitCode})",
                        Confidence = 0.0,
                        ErrorMessage = string.IsNullOrWhiteSpace(stderr)
                                        ? "Python からのエラーメッセージはありません（JSONも未取得）"
                                        : stderr.Trim()
                    };
                }

                // 終了コード0だが JSON がない
                return new NeedleDetectionResult
                {
                    DetectionMethod = "Python出力にJSONが見つからない",
                    Confidence = 0.0,
                    ErrorMessage = "Pythonの標準出力にJSONが見つかりませんでした。"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DetectNeedleWithPython例外: {ex.Message}");
                Debug.WriteLine($"スタックトレース: {ex.StackTrace}");

                return new NeedleDetectionResult
                {
                    DetectionMethod = "Python処理失敗: " + ex.Message,
                    Confidence = 0.0,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                Debug.WriteLine("=== DetectNeedleWithPython 終了 ===");
            }
        }



        #endregion

        #region 画面入力制御

        /// <summary>
        /// 画面入力項目のロック
        /// </summary>
        /// <returns></returns>
        private void InputLock()
        {
            btnShot.Enabled = false;
            btnSelectImage.Enabled = false;
            btnRTSP.Enabled = false;
        }

        /// <summary>
        /// 画面入力項目のロック解除
        /// </summary>
        /// <returns></returns>
        private void InputUnLock()
        {
            btnShot.Enabled = true;
            btnSelectImage.Enabled = true;
            btnRTSP.Enabled = true;
        }
        #endregion

        /// <summary>
        /// フォーム終了時時の処理
        /// </summary>
        private void FrmShot_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // リソースの解放
            pbCroppedImage.Image?.Dispose();
            pbCroppedImage.Image?.Dispose();
        }
    }
}