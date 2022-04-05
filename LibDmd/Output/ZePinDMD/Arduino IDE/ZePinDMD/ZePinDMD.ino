// ------------------------------------------ ZePinDMD by Zedrummer (http://pincabpassion.net)---------------------------------------------
// - Install the ESP32 board in Arduino IDE as explained here https://randomnerdtutorials.com/installing-the-esp32-board-in-arduino-ide-windows-instructions/
// - Install SPIFFS file system as explained here https://randomnerdtutorials.com/install-esp32-filesystem-uploader-arduino-ide/
// - Install "ESP32 HUB75 LED MATRIX PANNEL DMA" Display library via the library manager
// - Go to menu "Tools" then click on "ESP32 Sketch Data Upload"
// - Inject this code in the board
// - If you have blurry pictures, the display is not clean, try to reduce the input voltage of your LED matrix pannels, often, 5V pannels need
// between 4V and 4.5V to display clean pictures, you often have a screw in switch-mode power supplies to change the output voltage a little bit
// - While the initial pattern logo is displayed, check you have red in the upper left, green in the lower left and blue in the upper right,
// if not, make contact between the ORDRE_BUTTON_PIN (default 21, but you can change below) pin and a ground pin several times
// until the display is correct (automatically saved, no need to do it again)
// -----------------------------------------------------------------------------------------------------------------------------------------

#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#include <SPIFFS.h>

#define PANEL_WIDTH 64
#define PANEL_HEIGHT 32   
#define PANELS_NUMBER 2   // Number of chained panels

/* Pinout from ESP32-HUB75-MatrixPanel-I2S-DMA.h
    #define R1_PIN_DEFAULT  25
    #define G1_PIN_DEFAULT  26
    #define B1_PIN_DEFAULT  27
    #define R2_PIN_DEFAULT  14
    #define G2_PIN_DEFAULT  12
    #define B2_PIN_DEFAULT  13

    #define A_PIN_DEFAULT   23
    #define B_PIN_DEFAULT   19
    #define C_PIN_DEFAULT   5
    #define D_PIN_DEFAULT   17
    #define E_PIN_DEFAULT   -1 // IMPORTANT: Change to a valid pin if using a 64x64px panel.
              
    #define LAT_PIN_DEFAULT 4
    #define OE_PIN_DEFAULT  15
    #define CLK_PIN_DEFAULT 16
 */

int ordreRGB[3*6]={0,1,2, 2,0,1, 1,2,0,
                   0,2,1, 1,0,2, 2,1,0};
int acordreRGB=0;

#define PANE_WIDTH PANEL_WIDTH * PANELS_NUMBER
#define PANE_HEIGHT PANEL_HEIGHT

unsigned char Palette4[3*4]; // palettes 4 couleurs et 16 couleurs en RGB
static const float levels4[4]  = {10,33,67,100};
unsigned char Palette16[3*16];
static const float levels16[16]  = {0, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100}; // SAM brightness seems okay
unsigned char Palette64[3*64];
static const float levels64[64]  = {0, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100}; // SAM brightness seems okay

// placeholder for the matrix object
MatrixPanel_I2S_DMA *dma_display = nullptr;

unsigned char pannel[128*32*3];

#define ORDRE_BUTTON_PIN 21
bool OrdreBtnRel=false;
int OrdreBtnPos;
unsigned long OrdreBtnDebounceTime;

#define DEBOUNCE_DELAY 100 // in ms, to avoid buttons pushes to be counted several times https://www.arduino.cc/en/Tutorial/BuiltInExamples/Debounce
unsigned char CheckButton(int btnpin,bool *pbtnrel,int *pbtpos,unsigned long *pbtdebouncetime)
{
  // return 1 if the button has been released, 2 if the button has been pushed, 0 if nothing has changed since previous call
  // Debounce the input as explained here https://www.arduino.cc/en/Tutorial/BuiltInExamples/Debounce
  int btnPos=digitalRead(btnpin);
  unsigned long actime=millis();
  if (btnPos != (*pbtpos))
  {
    if (actime > (*pbtdebouncetime) + DEBOUNCE_DELAY)
    {
      if ((btnPos == HIGH) && ((*pbtnrel) == false))
      {
        (*pbtnrel) = true;
        (*pbtdebouncetime) = actime;
        (*pbtpos) = btnPos;
        return 1;
      }
      else if ((btnPos == LOW) && ((*pbtnrel) == true))
      {
        (*pbtnrel) = false;
        (*pbtdebouncetime) = actime;
        (*pbtpos) = btnPos;
        return 2;
      }
      (*pbtdebouncetime) = actime;
      (*pbtpos) = btnPos;
    }
  }
  return 0;
}

void fillpannel()
{
  for (int tj = 0; tj < 32; tj++)
  {
    for (int ti = 0; ti < 128; ti++)
    {
      
      
      
      
      
      
      
      
      
      dma_display->drawPixelRGB888(ti, tj, pannel[ti * 3 + tj * 3 * 128 + ordreRGB[acordreRGB * 3]], pannel[ti * 3 + tj * 3 * 128 + ordreRGB[acordreRGB * 3 + 1]], pannel[ti * 3 + tj * 3 * 128 + ordreRGB[acordreRGB * 3 + 2]]);








      
    }
  }
}

File fordre;

void LoadOrdreRGB()
{
  fordre=SPIFFS.open("/ordrergb.val");
  if (!fordre) return;
  acordreRGB=fordre.read();
  fordre.close();
}

void SaveOrdreRGB()
{
  fordre=SPIFFS.open("/ordrergb.val","w");
  fordre.write(acordreRGB);
  fordre.close();
}

void DisplayLogo(void)
{
  File flogo= SPIFFS.open("/logo.raw");
  if (!flogo) {
    //Serial.println("Failed to open file for reading");
    return;
  }
  for (unsigned int tj = 0; tj < 32; tj++)
  {
    for (unsigned int ti = 0; ti < 128; ti++)
    {
      pannel[ti * 3 + tj * 3 * 128] = flogo.read();
      pannel[ti * 3 + tj * 3 * 128 + 1] = flogo.read();
      pannel[ti * 3 + tj * 3 * 128 + 2] = flogo.read();
    }
  }
  fillpannel();
  flogo.close();
}

void InitPalettes(int R, int G, int B)
{
  // initialise les palettes à partir d'une couleur qui représente le 100%
  for (int ti = 0; ti < 4; ti++)
  {
    Palette4[ti * 3] = (unsigned char)((float)R*levels4[ti] / 100.0f);
    Palette4[ti * 3 + 1] = (unsigned char)((float)G*levels4[ti] / 100.0f);
    Palette4[ti * 3 + 2] = (unsigned char)((float)B*levels4[ti] / 100.0f);
  }
  for (int ti = 0; ti < 16; ti++)
  {
    Palette16[ti * 3] = (unsigned char)((float)R*levels16[ti] / 100.0f);
    Palette16[ti * 3 + 1] = (unsigned char)((float)G*levels16[ti] / 100.0f);
    Palette16[ti * 3 + 2] = (unsigned char)((float)B*levels16[ti] / 100.0f);
  }
  for (int ti = 0; ti < 64; ti++)
  {
    Palette64[ti * 3] = (unsigned char)((float)R*levels64[ti] / 100.0f);
    Palette64[ti * 3 + 1] = (unsigned char)((float)G*levels64[ti] / 100.0f);
    Palette64[ti * 3 + 2] = (unsigned char)((float)B*levels64[ti] / 100.0f);
  }
}

bool MireActive=true;

void setup()
{
  Serial.begin(921600);
  Serial.setRxBufferSize(4048);
  if (!SPIFFS.begin(true)) return;

  pinMode(ORDRE_BUTTON_PIN, INPUT_PULLUP);
    
  HUB75_I2S_CFG mxconfig;
  mxconfig.mx_height = PANEL_HEIGHT;      // we have 32 pix high panels
  mxconfig.mx_width = PANEL_WIDTH;      // we have 64 pix wide panels
  mxconfig.chain_length = PANELS_NUMBER;  // we have 2 pannels chained
  mxconfig.clkphase = false; // change if you have some parts of the pannel with a shift
  dma_display = new MatrixPanel_I2S_DMA(mxconfig);
  dma_display->begin();
  dma_display->setBrightness8(90);    // range is 0-255, 0 - 0%, 255 - 100%
  dma_display->clearScreen();

  LoadOrdreRGB();
  
  DisplayLogo();

  InitPalettes(255,109,0);
}

void loop()
{
  while (MireActive == true)
  {
    if (CheckButton(ORDRE_BUTTON_PIN, &OrdreBtnRel, &OrdreBtnPos, &OrdreBtnDebounceTime) == 2)
    {
      acordreRGB++;
      if (acordreRGB >= 6) acordreRGB = 0;
      SaveOrdreRGB();
      fillpannel();
    }
    if (Serial.available())
    {
      MireActive = false;
    }
  }
  int c1, c2, c3, c4;
  while (!Serial.available());
  c1 = Serial.read();
  while (!Serial.available());
  c2 = Serial.read();
  while (!Serial.available());
  c3 = Serial.read();
  while (!Serial.available());
  c4 = Serial.read();
  while ((c1 != 0x81) || (c2 != 0xC3) || (c3 != 0xE7) || (c4 < 0))
  {
    c1 = c2;
    c2 = c3;
    c3 = c4;
    while (!Serial.available());
    c4 = Serial.read();
  }
  if (c4==15) // shake hands with the computer
  {
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 6) // reinit palettes
  {
    InitPalettes(255, 109, 0);
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }    
  else if (c4 == 10) // clear screen
  {
    dma_display->clearScreen();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 3)
  {
    while (Serial.available() < min(4000,8*128*3));
    for (unsigned int ti = 0; ti < 16; ti++)
    {
      for (unsigned int tj = 0; tj < 128; tj++)
      {
        pannel[ti * 128 * 3 + tj * 3] = Serial.read();
        pannel[ti * 128 * 3 + tj * 3 + 1] = Serial.read();
        pannel[ti * 128 * 3 + tj * 3 + 2] = Serial.read();
      }
    }
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
    while (Serial.available() < min(4000,8*128*3));
    for (unsigned int ti = 16; ti < 32; ti++)
    {
      for (unsigned int tj = 0; tj < 128; tj++)
      {
        pannel[ti * 128 * 3 + tj * 3] = Serial.read();
        pannel[ti * 128 * 3 + tj * 3 + 1] = Serial.read();
        pannel[ti * 128 * 3 + tj * 3 + 2] = Serial.read();
      }
    }
    fillpannel();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 8) // mode 4 couleurs avec 1 palette 4 couleurs (4*3 bytes) suivis de 4 pixels par byte
  {
    while (Serial.available() < min(4000,3 * 4 + 2 * 512));
    for (int ti = 3; ti >= 0; ti--)
    {
      //while(!Serial.available());
      Palette4[ti * 3] = Serial.read();
      //while(!Serial.available());
      Palette4[ti * 3 + 1] = Serial.read();
      //while(!Serial.available());
      Palette4[ti * 3 + 2] = Serial.read();
    }
    unsigned char img[2 * 512];
    for (int tk = 0; tk < 2 * 512; tk++)
    {
      //while(!Serial.available());
      img[tk] = Serial.read();
    }
    for (int tj = 0; tj < 32; tj++)
    {
      for (int ti = 0; ti < 128 / 8; ti++)
      {
        unsigned char mask = 1;
        unsigned char planes[2];
        planes[0] = img[ti + tj * 16];
        planes[1] = img[512 + ti + tj * 16];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3] = Palette4[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 1] = Palette4[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 2] = Palette4[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 7) // mode 16 couleurs avec 1 palette 4 couleurs (4*3 bytes) suivis de 2 pixels par byte
  {
    while (Serial.available() < min(4000,3 * 4 + 4 * 512));
    // on lit la palette
    for (int ti = 3; ti >= 0; ti--)
    {
      //while(!Serial.available());
      Palette16[ti * 3] = Serial.read();
      //while(!Serial.available());
      Palette16[ti * 3 + 1] = Serial.read();
      //while(!Serial.available());
      Palette16[ti * 3 + 2] = Serial.read();
    }
    // on lit les points
    unsigned char img[4 * 512];
    for (int tk = 0; tk < 4 * 512; tk++)
    {
      //while(!Serial.available());
      img[tk] = Serial.read();
    }
    for (int tj = 0; tj < 32; tj++)
    {
      for (int ti = 0; ti < 128 / 8; ti++)
      {
        unsigned char mask = 1;
        unsigned char planes[4];
        planes[0] = img[ti + tj * 16];
        planes[1] = img[512 + ti + tj * 16];
        planes[2] = img[1024 + ti + tj * 16];
        planes[3] = img[1536 + ti + tj * 16];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          if ((planes[2] & mask) > 0) idx |= 4;
          if ((planes[3] & mask) > 0) idx |= 8;
          float fvalue = (float)idx / 4.0f;
          float fvalueR = (float)Palette16[((int)fvalue + 1) * 3] * (fvalue - (int)fvalue) + (float)Palette16[((int)fvalue) * 3] * (1.0f - (fvalue - (int)fvalue));
          if (fvalueR>255) fvalueR=255.0f; else if (fvalueR<0) fvalueR=0.0f;
          float fvalueG = (float)Palette16[((int)fvalue + 1) * 3 + 1] * (fvalue - (int)fvalue) + (float)Palette16[((int)fvalue) * 3 + 1] * (1.0f - (fvalue - (int)fvalue));
          if (fvalueG>255) fvalueG=255.0f; else if (fvalueG<0) fvalueG=0.0f;
          float fvalueB = (float)Palette16[((int)fvalue + 1) * 3 + 2] * (fvalue - (int)fvalue) + (float)Palette16[((int)fvalue) * 3 + 2] * (1.0f - (fvalue - (int)fvalue));
          if (fvalueB>255) fvalueB=255.0f; else if (fvalueB<0) fvalueB=0.0f;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3] = (int)fvalueR;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 1] = (int)fvalueG;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 2] = (int)fvalueB;
          mask <<= 1;
        }
      }
    }
    fillpannel();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 9) // mode 16 couleurs avec 1 palette 16 couleurs (16*3 bytes) suivis de 4 bytes par groupe de 8 points (séparés en plans de bits 4*512 bytes)
  {
    while (Serial.available() < min(4000,3 * 16 + 4 * 512));
    // on lit la palette
    for (int ti = 0; ti < 16; ti++)
    {
      //while(!Serial.available());
      Palette16[ti * 3] = Serial.read();
      //while(!Serial.available());
      Palette16[ti * 3 + 1] = Serial.read();
      //while(!Serial.available());
      Palette16[ti * 3 + 2] = Serial.read();
    }
    // on lit les points
    unsigned char img[4 * 512];
    for (int tk = 0; tk < 4 * 512; tk++)
    {
      //while(!Serial.available());
      img[tk] = Serial.read();
    }
    for (int tj = 0; tj < 32; tj++)
    {
      for (int ti = 0; ti < 128 / 8; ti++)
      {
        // on reconstitue un indice à partir des plans puis une couleur à partir de la palette
        unsigned char mask = 1;
        unsigned char planes[4];
        planes[0] = img[ti + tj * 16];
        planes[1] = img[512 + ti + tj * 16];
        planes[2] = img[1024 + ti + tj * 16];
        planes[3] = img[1536 + ti + tj * 16];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          if ((planes[2] & mask) > 0) idx |= 4;
          if ((planes[3] & mask) > 0) idx |= 8;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3] = Palette16[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 1] = Palette16[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 2] = Palette16[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
  else if (c4 == 11) // mode 64 couleurs avec 1 palette 64 couleurs (64*3 bytes) suivis de 6 bytes par groupe de 8 points (séparés en plans de bits 6*512 bytes)
  {
    while (Serial.available() < min(4000,3 * 16 + 6 * 512));
    // on lit la palette
    for (int ti = 0; ti < 64; ti++)
    {
      //while(!Serial.available());
      Palette64[ti * 3] = Serial.read();
      //while(!Serial.available());
      Palette64[ti * 3 + 1] = Serial.read();
      //while(!Serial.available());
      Palette64[ti * 3 + 2] = Serial.read();
    }
    // on lit les points
    unsigned char img[6 * 512];
    for (int tk = 0; tk < 6 * 512; tk++)
    {
      img[tk] = Serial.read();
    }
    for (int tj = 0; tj < 32; tj++)
    {
      for (int ti = 0; ti < 128 / 8; ti++)
      {
        // on reconstitue un indice à partir des plans puis une couleur à partir de la palette
        unsigned char mask = 1;
        unsigned char planes[6];
        planes[0] = img[ti + tj * 16];
        planes[1] = img[512 + ti + tj * 16];
        planes[2] = img[1024 + ti + tj * 16];
        planes[3] = img[1536 + ti + tj * 16];
        planes[4] = img[2048 + ti + tj * 16];
        planes[5] = img[2560 + ti + tj * 16];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          if ((planes[2] & mask) > 0) idx |= 4;
          if ((planes[3] & mask) > 0) idx |= 8;
          if ((planes[4] & mask) > 0) idx |= 0x10;
          if ((planes[5] & mask) > 0) idx |= 0x20;
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3] = Palette64[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 1] = Palette64[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * 128 * 3 + 2] = Palette64[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
    Serial.write(0x81);
    Serial.write(0xC3);
    Serial.write(0xE7);
    Serial.write(15);
  }
}
