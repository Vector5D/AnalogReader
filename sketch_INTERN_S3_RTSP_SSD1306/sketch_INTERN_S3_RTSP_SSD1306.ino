#include <WiFi.h>
#include <WebServer.h>
#include <WiFiClient.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include "XPowersLib.h"
#include "OV2640.h"
#include "OV2640Streamer.h"
#include "CRtspSession.h"

// 電源
#define XPOWERS_CHIP_AXP2101
XPowersAXP2101 PMU;

// OLED(スクリーン)設定
#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define SCREEN_ADDRESS 0x3C
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire);


// Wi-Fi設定
const char* ssid = "IODATA-67ea9c-2G";
const char* password = "R7msv46861597";

//  LilyGO T-Camera ESP32-S3 用ピン定義
#define PWDN_GPIO_NUM    -1
#define RESET_GPIO_NUM   39
#define XCLK_GPIO_NUM    38
#define SIOD_GPIO_NUM     5
#define SIOC_GPIO_NUM     4
#define Y9_GPIO_NUM       9
#define Y8_GPIO_NUM      10
#define Y7_GPIO_NUM      11
#define Y6_GPIO_NUM      13
#define Y5_GPIO_NUM      21
#define Y4_GPIO_NUM      48
#define Y3_GPIO_NUM      47
#define Y2_GPIO_NUM      14
#define VSYNC_GPIO_NUM    8
#define HREF_GPIO_NUM    18
#define PCLK_GPIO_NUM    12

OV2640 cam;
WebServer server(80);
WiFiServer rtspServer(8554);
CStreamer* streamer = nullptr;
CRtspSession* session = nullptr;
WiFiClient rtspClient;


void setup() {
  Serial.begin(115200);
  delay(1000);

  // PMU(電源チップ)の初期化
  Wire.begin(7, 6);
  if (!PMU.begin(Wire, AXP2101_SLAVE_ADDRESS, 7, 6)) {
    Serial.println("Failed to initialize PMU");
    while (true) delay(1000);
  }

  // OLED(スクリーン)の初期化
  display.begin(SSD1306_SWITCHCAPVCC, SCREEN_ADDRESS);
  display.clearDisplay();
  display.display();

  // カメラ用電源をON
  PMU.setALDO1Voltage(1800); // DVDD: 1.8V
  PMU.enableALDO1();

  PMU.setALDO2Voltage(2800); // DOVDD: 2.8V
  PMU.enableALDO2();

  PMU.setALDO4Voltage(3000); // AVDD: 3.0V
  PMU.enableALDO4();

  // TSピンを無効化（充電の誤検出を防ぐ）
  PMU.disableTSPinMeasure();

  // カメラ設定
  camera_config_t config;
  config.ledc_channel = LEDC_CHANNEL_0;
  config.ledc_timer   = LEDC_TIMER_0;
  config.pin_d0       = Y2_GPIO_NUM;
  config.pin_d1       = Y3_GPIO_NUM;
  config.pin_d2       = Y4_GPIO_NUM;
  config.pin_d3       = Y5_GPIO_NUM;
  config.pin_d4       = Y6_GPIO_NUM;
  config.pin_d5       = Y7_GPIO_NUM;
  config.pin_d6       = Y8_GPIO_NUM;
  config.pin_d7       = Y9_GPIO_NUM;
  config.pin_xclk     = XCLK_GPIO_NUM;
  config.pin_pclk     = PCLK_GPIO_NUM;
  config.pin_vsync    = VSYNC_GPIO_NUM;
  config.pin_href     = HREF_GPIO_NUM;
  config.pin_sccb_sda = SIOD_GPIO_NUM;
  config.pin_sccb_scl = SIOC_GPIO_NUM;
  config.pin_pwdn     = PWDN_GPIO_NUM;
  config.pin_reset    = RESET_GPIO_NUM;
  config.xclk_freq_hz = 20000000;
  config.pixel_format = PIXFORMAT_JPEG;
  config.frame_size = FRAMESIZE_SXGA;
  config.fb_location = CAMERA_FB_IN_PSRAM;
  config.jpeg_quality = 12;
  config.fb_count     = 2;

  // カメラ初期化
  esp_err_t err = cam.init(config);
  if (err != ESP_OK) {
    Serial.printf("Camera init failed! Error code: 0x%x\n", err);
    while (true) delay(1000);
  }
  Serial.println("Camera initialized successfully");


  // Wi-Fi STAモードで接続
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);
  Serial.print("Connecting to Wi-Fi");

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  // シリアルモニタ表示
  Serial.println("\nWi-Fi connected!");
  Serial.print("IP address: ");
  Serial.println(WiFi.localIP());

  // スクリーンにIPアドレス表示
  display.clearDisplay();
  display.setTextSize(2);
  display.setTextColor(SSD1306_WHITE);
  display.setCursor(0, 0);
  display.println("IP: ");
  display.println(WiFi.localIP());
  display.display();

  // RTSPサーバー開始 
  rtspServer.begin();
  streamer = new OV2640Streamer(&cam);

  // Webサーバー（テスト確認用）
  server.on("/", []() {
    server.send(200, "text/plain", "ESP32-CAM with RTSP + OLED");
  });
  server.begin();
  Serial.println("HTTP server started");
}

void loop() {
  server.handleClient();
  static uint32_t lastimage = millis();
  uint32_t now = millis();
  uint32_t msecPerFrame = 67;

  if (session) {
    session->handleRequests(0);  // RTSPコマンド処理

    // 一定時間ごとに画像送信
    if (now > lastimage + msecPerFrame || now < lastimage) {
      streamer->streamImage(now);
      lastimage = now;
    }

    // セッション終了チェック
    if (session->m_stopped) {
      delete session;
      session = nullptr;
      rtspClient.stop();
    }
  } else {
    // クライアント受け入れ
    rtspClient = rtspServer.accept();
    if (rtspClient) {
      Serial.print("RTSP client connected: ");
      Serial.println(rtspClient.remoteIP());
      session = new CRtspSession(&rtspClient, streamer);
    }
  }
}
