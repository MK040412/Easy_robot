// 하드웨어 테스트 코드
// 조이스틱 2개, 버튼 4개 작동 확인용
// 메인 코드와 동일한 핀 매핑 사용

#include <Arduino.h>

// ====== Pins (메인 코드와 동일) ======
// Analog - X와 Y 바뀜
static const uint8_t PIN_J1_X = A1;  
static const uint8_t PIN_J1_Y = A0; 
static const uint8_t PIN_J2_X = A3;  
static const uint8_t PIN_J2_Y = A2;  

// Buttons (INPUT_PULLUP)
static const uint8_t PIN_BTN_A = 2;
static const uint8_t PIN_BTN_B = 3;
static const uint8_t PIN_BTN_X = 4;
static const uint8_t PIN_BTN_Y = 5;

void setup() {
  Serial.begin(115200);
  
  // 버튼 설정
  pinMode(PIN_BTN_A, INPUT_PULLUP);
  pinMode(PIN_BTN_B, INPUT_PULLUP);
  pinMode(PIN_BTN_X, INPUT_PULLUP);
  pinMode(PIN_BTN_Y, INPUT_PULLUP);
  
  Serial.println("=== Arduino Controller Test ===");
  Serial.println("핀 매핑: J1(A1,A0) J2(A3,A2) - Y축 반전 적용");
  Serial.println("조이스틱을 움직이고 버튼을 눌러보세요");
  Serial.println("================================");
  delay(1000);
}

void loop() {
  // 아날로그 원시값 읽기
  int raw_j1x = analogRead(PIN_J1_X);  // A1
  int raw_j1y = analogRead(PIN_J1_Y);  // A0
  int raw_j2x = analogRead(PIN_J2_X);  // A3
  int raw_j2y = analogRead(PIN_J2_Y);  // A2
  
  // X축 반전 적용 (메인 코드와 동일)
  int j1x = 1023 - raw_j1x;
  int j1y = raw_j1y;  
  int j2x = 1023 - raw_j2x;
  int j2y = raw_j2y;  
  
  // 버튼 읽기 (LOW = 눌림)
  bool btnA = !digitalRead(PIN_BTN_A);
  bool btnB = !digitalRead(PIN_BTN_B);
  bool btnX = !digitalRead(PIN_BTN_X);
  bool btnY = !digitalRead(PIN_BTN_Y);
  
  // === 기본 출력 ===
  Serial.print("J1[");
  Serial.print(j1x);
  Serial.print(",");
  Serial.print(j1y);
  Serial.print("]");
  
  // 조이스틱1 방향 표시
  if (j1x < 400) Serial.print("◀");
  else if (j1x > 600) Serial.print("▶");
  else Serial.print(" ");
  
  if (j1y < 400) Serial.print("▲");
  else if (j1y > 600) Serial.print("▼");
  else Serial.print(" ");
  
  Serial.print(" | J2[");
  Serial.print(j2x);
  Serial.print(",");
  Serial.print(j2y);
  Serial.print("]");
  
  // 조이스틱2 방향 표시
  if (j2x < 400) Serial.print("◀");
  else if (j2x > 600) Serial.print("▶");
  else Serial.print(" ");
  
  if (j2y < 400) Serial.print("▲");
  else if (j2y > 600) Serial.print("▼");
  else Serial.print(" ");
  
  Serial.print(" | BTN:");
  Serial.print(btnA ? "Ⓐ" : "□");
  Serial.print(btnB ? "Ⓑ" : "□");
  Serial.print(btnX ? "Ⓧ" : "□");
  Serial.print(btnY ? "Ⓨ" : "□");
  
  // === 진단 정보 ===
  Serial.print(" | ");
  
  // 원시값 표시 (디버깅용)
  Serial.print("RAW[");
  Serial.print(raw_j1y);  // A0
  Serial.print(",");
  Serial.print(raw_j2y);  // A2
  Serial.print("]");
  
  // 조이스틱 문제 체크
  if (j1x < 10 || j1x > 1013 || j1y < 10 || j1y > 1013) {
    Serial.print("[J1극값!]");
  }
  if (j2x < 10 || j2x > 1013 || j2y < 10 || j2y > 1013) {
    Serial.print("[J2극값!]");
  }
  
  // 중립값 체크
  bool j1_neutral = (j1x > 450 && j1x < 550 && j1y > 450 && j1y < 550);
  bool j2_neutral = (j2x > 450 && j2x < 550 && j2y > 450 && j2y < 550);
  
  if (j1_neutral && j2_neutral && !btnA && !btnB && !btnX && !btnY) {
    Serial.print(" 대기");
  }
  
  Serial.println();
  delay(100);  // 100ms 딜레이
}
