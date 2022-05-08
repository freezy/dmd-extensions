#define PANEL_WIDTH 64 // width: number of LEDs for 1 pannel
#define PANEL_HEIGHT 32 // height: number of LEDs
#define PANELS_NUMBER 2   // Number of horizontally chained panels 
// ------------------------------------------ ZeDMD by Zedrummer (http://pincabpassion.net)---------------------------------------------
// - Install the ESP32 board in Arduino IDE as explained here https://randomnerdtutorials.com/installing-the-esp32-board-in-arduino-ide-windows-instructions/
// - Install SPIFFS file system as explained here https://randomnerdtutorials.com/install-esp32-filesystem-uploader-arduino-ide/
// - Install "ESP32 HUB75 LED MATRIX PANNEL DMA" Display library via the library manager
// - Go to menu "Tools" then click on "ESP32 Sketch Data Upload"
// - Change the values in the 3 first lines above (PANEL_WIDTH, PANEL_HEIGHT, PANELS_NUMBER) according the pannels
// - Inject this code in the board
// - If you have blurry pictures, the display is not clean, try to reduce the input voltage of your LED matrix pannels, often, 5V pannels need
// between 4V and 4.5V to display clean pictures, you often have a screw in switch-mode power supplies to change the output voltage a little bit
// - While the initial pattern logo is displayed, check you have red in the upper left, green in the lower left and blue in the upper right,
// if not, make contact between the ORDRE_BUTTON_PIN (default 21, but you can change below) pin and a ground pin several times
// until the display is correct (automatically saved, no need to do it again)
// -----------------------------------------------------------------------------------------------------------------------------------------

#define PANE_WIDTH (PANEL_WIDTH*PANELS_NUMBER)
#define PANE_WIDTH_PLANE (PANE_WIDTH>>3)
#define PANE_HEIGHT PANEL_HEIGHT

#include <ESP32-HUB75-MatrixPanel-I2S-DMA.h>
#include <SPIFFS.h>

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

// Change these to whatever suits
#define R1_PIN 25
#define G1_PIN 26
#define B1_PIN 27
#define R2_PIN 14
#define G2_PIN 12
#define B2_PIN 13
#define A_PIN 23
#define B_PIN 19
#define C_PIN 5
#define D_PIN 17
#define E_PIN 22 // required for 1/32 scan panels, like 64x64. Any available pin would do, i.e. IO32. If 1/16 scan panels, no connection to this pin needed
#define LAT_PIN 4
#define OE_PIN 15
#define CLK_PIN 16

#define N_CTRL_CHARS 6
#define N_INTERMEDIATE_CTR_CHARS 4
// !!!!! PAS IDENTIQUE !!!!!
unsigned char CtrlCharacters[6]={0x5a,0x65,0x64,0x72,0x75,0x6d}; 
// !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

bool min_chiffres[3*10*5]={0,1,0, 0,0,1, 1,1,0, 1,1,0, 0,0,1, 1,1,1, 0,1,1, 1,1,1, 0,1,0, 0,1,0,
                           1,0,1, 0,1,1, 0,0,1, 0,0,1, 0,1,0, 1,0,0, 1,0,0, 0,0,1, 1,0,1, 1,0,1,
                           1,0,1, 0,0,1, 0,1,0, 0,1,0, 1,1,1, 1,1,0, 1,1,0, 0,1,0, 0,1,0, 0,1,1,
                           1,0,1, 0,0,1, 1,0,0, 0,0,1, 0,0,1, 0,0,1, 1,0,1, 1,0,0, 1,0,1, 0,0,1,
                           0,1,0, 0,0,1, 1,1,1, 1,1,0, 0,0,1, 1,1,0, 0,1,0, 1,0,0, 0,1,0, 1,1,0};

bool lumtxt[16*5]={0,1,0,0,0,1,0,0,1,0,1,1,0,1,1,0,
                   0,1,0,0,0,1,0,0,1,0,1,0,1,0,1,0,
                   0,1,0,0,0,1,0,0,1,0,1,0,0,0,1,0,
                   0,1,0,0,0,1,0,0,1,0,1,0,0,0,1,0,
                   0,1,1,1,0,0,1,1,1,0,1,0,0,0,1,0};



HUB75_I2S_CFG::i2s_pins _pins={R1_PIN, G1_PIN, B1_PIN, R2_PIN, G2_PIN, B2_PIN, A_PIN, B_PIN, C_PIN, D_PIN, E_PIN, LAT_PIN, OE_PIN, CLK_PIN};
HUB75_I2S_CFG mxconfig(
          PANEL_WIDTH,   // width
          PANEL_HEIGHT,   // height
           PANELS_NUMBER,   // chain length
         _pins//,   // pin mapping
         //HUB75_I2S_CFG::FM6126A      // driver chip
);
MatrixPanel_I2S_DMA *dma_display = nullptr;

int ordreRGB[3*6]={0,1,2, 2,0,1, 1,2,0,
                   0,2,1, 1,0,2, 2,1,0};
int acordreRGB=0;

unsigned char Palette4[3*4]; // palettes 4 couleurs et 16 couleurs en RGB
static const float levels4[4]  = {10,33,67,100};
unsigned char Palette16[3*16];
static const float levels16[16]  = {0, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100}; // SAM brightness seems okay
unsigned char Palette64[3*64];
static const float levels64[64]  = {0, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 100}; // SAM brightness seems okay

unsigned char pannel[PANE_WIDTH*PANE_HEIGHT*3];

#define ORDRE_BUTTON_PIN 21
bool OrdreBtnRel=false;
int OrdreBtnPos;
unsigned long OrdreBtnDebounceTime;

#define LUMINOSITE_BUTTON_PIN 33
bool LuminositeBtnRel=false;
int LuminositeBtnPos;
unsigned long LuminositeBtnDebounceTime;

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

void DisplayChiffre(unsigned int chf, int x,int y,int R, int G, int B)
{
  // affiche un chiffre verticalement
  unsigned int c=chf%10;
  const int poscar=3*c;
  for (int ti=0;ti<5;ti++)
  {
    for (int tj=0;tj<4;tj++)
    {
      if (tj<3) {if (min_chiffres[poscar+tj+ti*3*10]==1) dma_display->drawPixelRGB888(x+tj,y+ti,R,G,B); else dma_display->drawPixelRGB888(x+tj,y+ti,0,0,0);}
      else dma_display->drawPixelRGB888(x+tj,y+ti,0,0,0);
    }
  }
}

void DisplayNombre(unsigned int chf,int x,int y,int R,int G,int B)
{
  // affiche un nombre verticalement
  unsigned int c=chf&0xff;
  unsigned int acc=c,acd=100;
  for (int ti=0;ti<3;ti++)
  {
    unsigned int val=(unsigned int)(acc/acd);
    DisplayChiffre(val,x+4*ti,y,R,G,B);
    acc=acc-val*acd;
    acd/=10;
  }
}

void DisplayNombre2(unsigned int chf,int x,int y,int R,int G,int B)
{
  // affiche un nombre verticalement
  unsigned int c=chf&0xffff;
  unsigned int acc=c,acd=10000;
  for (int ti=0;ti<5;ti++)
  {
    unsigned int val=(unsigned int)(acc/acd);
    DisplayChiffre(val,x+4*ti,y,R,G,B);
    acc=acc-val*acd;
    acd/=10;
  }
}

void DisplayNombre3(unsigned int chf,int x,int y,int R,int G,int B)
{
  // affiche un nombre verticalement
  unsigned int c=chf;
  unsigned int acc=c,acd=1000000000;
  for (int ti=0;ti<10;ti++)
  {
    unsigned int val=(unsigned int)(acc/acd);
    DisplayChiffre(val,x+4*ti,y,R,G,B);
    acc=acc-val*acd;
    acd/=10;
  }
}

int Luminosite=95;

void DisplayLum(void)
{
  DisplayNombre(Luminosite,PANE_WIDTH/2-16/2-3*4/2+16,PANE_HEIGHT-5,255,255,255);
}

void DisplayText(bool* text, int width, int x, int y, int R, int G, int B)
{
  // affiche le texte "SCORE" en (x, y)
  for (unsigned int ti=0;ti<width;ti++)
  {
    for (unsigned int tj=0; tj<5;tj++)
    {
      if (text[ti+tj*width]==1) dma_display->drawPixelRGB888(x+ti,y+tj,R,G,B); else dma_display->drawPixelRGB888(x+ti,y+tj,0,0,0);
    }
  }
}

void fillpannel()
{
  for (int tj = 0; tj < PANE_HEIGHT; tj++)
  {
    for (int ti = 0; ti < PANE_WIDTH; ti++)
    {
      dma_display->drawPixelRGB888(ti, tj, pannel[ti * 3 + tj * 3 * PANE_WIDTH + ordreRGB[acordreRGB * 3]], pannel[ti * 3 + tj * 3 * PANE_WIDTH + ordreRGB[acordreRGB * 3 + 1]], pannel[ti * 3 + tj * 3 * PANE_WIDTH + ordreRGB[acordreRGB * 3 + 2]]);
    }
  }
  //delayMicroseconds(6060);
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

File flum;

void LoadLum()
{
  flum=SPIFFS.open("/lum.val");
  if (!flum) return;
  Luminosite=flum.read();
  flum.close();
}

void SaveLum()
{
  flum=SPIFFS.open("/lum.val","w");
  flum.write(Luminosite);
  flum.close();
}

#define WIDTH_LOGO_FILE 128
#define HEIGHT_LOGO_FILE 32

unsigned char tempbuf[WIDTH_LOGO_FILE*HEIGHT_LOGO_FILE*3];

void DisplayLogo(void)
{
  const float X_LOGO_RATIO=(float)WIDTH_LOGO_FILE/(float)PANE_WIDTH;
  const float Y_LOGO_RATIO=(float)HEIGHT_LOGO_FILE/(float)PANE_HEIGHT;
  for (unsigned int tj = 0; tj < PANE_HEIGHT; tj++)
  {
    for (unsigned int ti = 0; ti < PANE_WIDTH; ti++)
    {
      float xpos=(float)ti*X_LOGO_RATIO;
      float ypos=(float)tj*Y_LOGO_RATIO;
      int xposL=(int)xpos;
      int yposU=(int)ypos;
      float xdeci=xpos-(float)xposL;
      float ydeci=ypos-(float)yposU;
      int idxUL=xposL*3+yposU*WIDTH_LOGO_FILE*3;
      int idxUR=idxUL+3;
      int idxDL=idxUL+WIDTH_LOGO_FILE*3;
      int idxDR=idxDL+3;
      float vU=(1.0f-xdeci)*tempbuf[idxUL]+xdeci*tempbuf[idxUR];
      float vD=(1.0f-xdeci)*tempbuf[idxDL]+xdeci*tempbuf[idxDR];
      float v=(unsigned char)((1.0f-ydeci)*vU+ydeci*vD);
      if (v>255) v=255.0f; else if (v<0) v=0.0f;
      pannel[ti * 3 + tj * 3 * PANE_WIDTH]=(int)v;
      vU=(1.0f-xdeci)*tempbuf[idxUL+1]+xdeci*tempbuf[idxUR+1];
      vD=(1.0f-xdeci)*tempbuf[idxDL+1]+xdeci*tempbuf[idxDR+1];
      v=(unsigned char)((1.0f-ydeci)*vU+ydeci*vD);
      if (v>255) v=255.0f; else if (v<0) v=0.0f;
      pannel[ti * 3 + tj * 3 * PANE_WIDTH+1]=(int)v;
      vU=(1.0f-xdeci)*tempbuf[idxUL+2]+xdeci*tempbuf[idxUR+2];
      vD=(1.0f-xdeci)*tempbuf[idxDL+2]+xdeci*tempbuf[idxDR+2];
      v=(unsigned char)((1.0f-ydeci)*vU+ydeci*vD);
      if (v>255) v=255.0f; else if (v<0) v=0.0f;
      pannel[ti * 3 + tj * 3 * PANE_WIDTH+2]=(int)v;
    }
  }
  fillpannel();
}

void LoadLogo(void)
{
  File flogo= SPIFFS.open("/logo.raw");
  if (!flogo) {
    //Serial.println("Failed to open file for reading");
    return;
  }
  for (unsigned int tj = 0; tj < HEIGHT_LOGO_FILE; tj++)
  {
    for (unsigned int ti = 0; ti < WIDTH_LOGO_FILE; ti++)
    {
      tempbuf[ti * 3 + tj * 3 * WIDTH_LOGO_FILE] = flogo.read();
      tempbuf[ti * 3 + tj * 3 * WIDTH_LOGO_FILE + 1] = flogo.read();
      tempbuf[ti * 3 + tj * 3 * WIDTH_LOGO_FILE + 2] = flogo.read();
    }
  }
  flogo.close();
  DisplayLogo();
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
#define SERIAL_TRANSFER_SIZE 9400
unsigned char img2[3*64+6 * PANE_WIDTH/8*PANE_HEIGHT];

void setup()
{
  Serial.begin(921600);
  Serial.setRxBufferSize(SERIAL_TRANSFER_SIZE+100);
  if (!SPIFFS.begin(true)) return;

  pinMode(ORDRE_BUTTON_PIN, INPUT_PULLUP);
  pinMode(LUMINOSITE_BUTTON_PIN, INPUT_PULLUP);
    
  mxconfig.clkphase = false; // change if you have some parts of the pannel with a shift
  dma_display = new MatrixPanel_I2S_DMA(mxconfig);
  dma_display->begin();
  LoadLum();
  dma_display->setBrightness8(Luminosite);    // range is 0-255, 0 - 0%, 255 - 100%
  dma_display->clearScreen();

  LoadOrdreRGB();

  LoadLogo();
  DisplayText(lumtxt,16,PANE_WIDTH/2-16/2-3*4/2,PANE_HEIGHT-5,255,255,255);
  DisplayLum();

  InitPalettes(255,109,0);

}

int SerialReadBuffer(unsigned char* pBuffer, unsigned int BufferSize)
{
  // 0 - ça s'est passé nickel
  // 1 - caractères de contrôle détectés
  // 255 - fin du buffer atteint
  memset(pBuffer, 0, BufferSize);
  unsigned int ptrB=0,iniptrB=0;
  unsigned int remBytes=BufferSize;
  unsigned char nCtrlCharFound=0,nCtrlIntCharFound=0;
  unsigned int c1=min((unsigned int)SERIAL_TRANSFER_SIZE-(unsigned int)N_CTRL_CHARS-1,remBytes);
  while (remBytes>0)
  {
    if (c1>500) while (Serial.available() < c1*1/2);
    for (int ti=0;ti<c1;ti++)
    {
      while (Serial.available()==0);
      pBuffer[ptrB]=Serial.read();
      // on vérifie si on ne reçoit pas une nouvelle image
      if (pBuffer[ptrB]==CtrlCharacters[nCtrlCharFound])
      {
        nCtrlCharFound++;
        if (nCtrlCharFound==N_CTRL_CHARS) return 1;
      }
      else nCtrlCharFound=0;
      // on vérifie si on n'est pas au début d'un nouveau transfert
      if (pBuffer[ptrB]==CtrlCharacters[N_INTERMEDIATE_CTR_CHARS-1-nCtrlIntCharFound]) // ctrl chars dans l'autre sens pour le début d'un nouveau transfert
      {
        nCtrlIntCharFound++;
        if (nCtrlIntCharFound==N_INTERMEDIATE_CTR_CHARS)
        {
          iniptrB+=c1;
          ptrB=iniptrB;
          break; // début du transfert suivant
        }
      }
      else nCtrlIntCharFound=0;
      ptrB++;
      // on vérifie si on n'a pas atteint la fin du buffer
      if (ptrB>=BufferSize)
      {
        //while (Serial.available()>0) Serial.read();
        return 255;
      }
    }
    remBytes-=c1;
    c1=min((unsigned int)SERIAL_TRANSFER_SIZE-(unsigned int)N_INTERMEDIATE_CTR_CHARS,remBytes);
    // on élimine les N_INTERMEDIATE_CTR_CHARS premiers caractères en supposant que ce sont les caractères de contrôle
    // on ne vérifie pas que ce sont les bons
    if (remBytes>0)
    {
      for (int ti=0;ti<N_INTERMEDIATE_CTR_CHARS;ti++)
      {
        while (Serial.available()==0);
        Serial.read();
      }
    }
    //while (Serial.available()>0) Serial.read();
  }
  return 0;
}

void SerialReadRLEBuffer(int FileSize)
{
  int remBytes=FileSize;
  int tj=0;
  int ptrB=0;
  unsigned char readbuf[4];
  while (remBytes>0)
  {
    int c1, c2, c3, c4;
    while (!Serial.available());
    c1 = Serial.read();
    while (!Serial.available());
    c2 = Serial.read();
    while (!Serial.available());
    c3 = Serial.read();
    while (!Serial.available());
    c4 = Serial.read();
    c1+=c2*256+c3*65536+c4*16777216;
    while (Serial.available() < min(SERIAL_TRANSFER_SIZE-256,c1*1/2));
    int ti=0;
    while (ti<c1)
    {
      readbuf[tj]=Serial.read();
      tj++;
      ti++;
      if (tj==4)
      {
        tj=0;
        while((ptrB<PANE_WIDTH*PANE_HEIGHT)&&(readbuf[0]>0))
        {
          pannel[ptrB*3]=readbuf[1];
          pannel[ptrB*3+1]=readbuf[2];
          pannel[ptrB*3+2]=readbuf[3];
          readbuf[0]--;
          ptrB++;
        }
      }
    }
    remBytes-=c1;
    while (Serial.available()) Serial.read();
  }
}

void Say(unsigned char where,unsigned int what)
{
  DisplayNombre(where,0,where*5,255,255,255);
  if (what!=(unsigned int)-1) DisplayNombre3(what,15,where*5,255,255,255);
  delay(1000);
}

unsigned int RetSRB=0;

void wait_for_new_image(void)
{
  unsigned char nCtrlCharFound=0;
  while (nCtrlCharFound<N_CTRL_CHARS)
  {
    while (Serial.available()==0);
    if (Serial.read()==CtrlCharacters[nCtrlCharFound]) nCtrlCharFound++; else nCtrlCharFound=0;
  }
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
    if (CheckButton(LUMINOSITE_BUTTON_PIN, &LuminositeBtnRel, &LuminositeBtnPos, &LuminositeBtnDebounceTime) == 2)
    {
      Luminosite+=10;
      if (Luminosite>255) Luminosite=15;
      dma_display->setBrightness8(Luminosite);
      DisplayLum();
      SaveLum();
    }
    if (Serial.available()>0)
    {
      dma_display->clearScreen();
      MireActive = false;
    }
  }
  if (RetSRB!=1) wait_for_new_image();
  unsigned char c4;
  while (Serial.available()==0);
  c4=Serial.read();
  if (c4 == 12) // ask for resolution (and shake hands)
  {
    for (int ti=0;ti<N_INTERMEDIATE_CTR_CHARS;ti++) Serial.write(CtrlCharacters[ti]);
    Serial.write(PANE_WIDTH&0xff);
    Serial.write((PANE_WIDTH>>8)&0xff);
    Serial.write(PANE_HEIGHT&0xff);
    Serial.write((PANE_HEIGHT>>8)&0xff);
  }
  else if (c4 == 6) // reinit palettes
  {
    InitPalettes(255, 109, 0);
  }    
  else if (c4 == 10) // clear screen
  {
    dma_display->clearScreen();
  }
  else if (c4 == 3)
  {
    RetSRB=SerialReadBuffer(pannel,PANE_WIDTH*PANE_HEIGHT*3);
    fillpannel();
  }
  else if (c4 == 13)
  {
    //while (Serial.available()) Serial.read();
    while (!Serial.available());
    unsigned int c1;
    c1 = (int)Serial.read();
    while (!Serial.available());
    c1 += (int)Serial.read()*256;
    while (!Serial.available());
    c1 += (int)Serial.read()*65536;
    while (!Serial.available());
    c1 += (int)Serial.read()*16777216;
    SerialReadRLEBuffer(c1);
    fillpannel();
  }
  else if (c4 == 8) // mode 4 couleurs avec 1 palette 4 couleurs (4*3 bytes) suivis de 4 pixels par byte
  {
    RetSRB=SerialReadBuffer(img2,3*4+2*PANE_WIDTH/8*PANE_HEIGHT);
    for (int ti = 3; ti >= 0; ti--)
    {
      Palette4[ti * 3] = img2[ti*3];
      Palette4[ti * 3 + 1] = img2[ti*3+1];
      Palette4[ti * 3 + 2] = img2[ti*3+2];
    }
    unsigned char* img=&img2[3*4];
    for (int tj = 0; tj < PANE_HEIGHT; tj++)
    {
      for (int ti = 0; ti < PANE_WIDTH / 8; ti++)
      {
        unsigned char mask = 1;
        unsigned char planes[2];
        planes[0] = img[ti + tj * PANE_WIDTH/8];
        planes[1] = img[PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3] = Palette4[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 1] = Palette4[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 2] = Palette4[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
  }
  else if (c4 == 7) // mode 16 couleurs avec 1 palette 4 couleurs (4*3 bytes) suivis de 2 pixels par byte
  {
    RetSRB=SerialReadBuffer(img2,3*4+4*PANE_WIDTH/8*PANE_HEIGHT);
    for (int ti = 3; ti >= 0; ti--)
    {
      Palette16[ti * 3] = img2[ti*3];
      Palette16[ti * 3 + 1] = img2[ti*3+1];
      Palette16[ti * 3 + 2] = img2[ti*3+2];
    }
    unsigned char* img=&img2[3*4];
    for (int tj = 0; tj < PANE_HEIGHT; tj++)
    {
      for (int ti = 0; ti < PANE_WIDTH / 8; ti++)
      {
        unsigned char mask = 1;
        unsigned char planes[4];
        planes[0] = img[ti + tj * PANE_WIDTH/8];
        planes[1] = img[PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[2] = img[2*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[3] = img[3*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
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
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3] = (int)fvalueR;
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 1] = (int)fvalueG;
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 2] = (int)fvalueB;
          mask <<= 1;
        }
      }
    }
    fillpannel();
  }
  else if (c4 == 9) // mode 16 couleurs avec 1 palette 16 couleurs (16*3 bytes) suivis de 4 bytes par groupe de 8 points (séparés en plans de bits 4*512 bytes)
  {
    RetSRB=SerialReadBuffer(img2,3*16+4*PANE_WIDTH/8*PANE_HEIGHT);
    for (int ti = 15; ti >= 0; ti--)
    {
      Palette16[ti * 3] = img2[ti*3];
      Palette16[ti * 3 + 1] = img2[ti*3+1];
      Palette16[ti * 3 + 2] = img2[ti*3+2];
    }
    unsigned char* img=&img2[3*16];
    for (int tj = 0; tj < PANE_HEIGHT; tj++)
    {
      for (int ti = 0; ti < PANE_WIDTH / 8; ti++)
      {
        // on reconstitue un indice à partir des plans puis une couleur à partir de la palette
        unsigned char mask = 1;
        unsigned char planes[4];
        planes[0] = img[ti + tj * PANE_WIDTH/8];
        planes[1] = img[PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[2] = img[2*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[3] = img[3*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          if ((planes[2] & mask) > 0) idx |= 4;
          if ((planes[3] & mask) > 0) idx |= 8;
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3] = Palette16[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 1] = Palette16[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 2] = Palette16[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
  }
  else if (c4 == 11) // mode 64 couleurs avec 1 palette 64 couleurs (64*3 bytes) suivis de 6 bytes par groupe de 8 points (séparés en plans de bits 6*512 bytes)
  {
    RetSRB=SerialReadBuffer(img2,3*64+6*PANE_WIDTH/8*PANE_HEIGHT);
    for (int ti = 63; ti >= 0; ti--)
    {
      Palette64[ti * 3] = img2[ti*3];
      Palette64[ti * 3 + 1] = img2[ti*3+1];
      Palette64[ti * 3 + 2] = img2[ti*3+2];
    }
    unsigned char* img=&img2[3*64];
    for (int tj = 0; tj < PANE_HEIGHT; tj++)
    {
      for (int ti = 0; ti < PANE_WIDTH / 8; ti++)
      {
        // on reconstitue un indice à partir des plans puis une couleur à partir de la palette
        unsigned char mask = 1;
        unsigned char planes[6];
        planes[0] = img[ti + tj * PANE_WIDTH/8];
        planes[1] = img[PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[2] = img[2*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[3] = img[3*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[4] = img[4*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        planes[5] = img[5*PANE_WIDTH_PLANE*PANE_HEIGHT + ti + tj * PANE_WIDTH_PLANE];
        for (int tk = 0; tk < 8; tk++)
        {
          unsigned char idx = 0;
          if ((planes[0] & mask) > 0) idx |= 1;
          if ((planes[1] & mask) > 0) idx |= 2;
          if ((planes[2] & mask) > 0) idx |= 4;
          if ((planes[3] & mask) > 0) idx |= 8;
          if ((planes[4] & mask) > 0) idx |= 0x10;
          if ((planes[5] & mask) > 0) idx |= 0x20;
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3] = Palette64[idx * 3];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 1] = Palette64[idx * 3 + 1];
          pannel[(ti * 8 + tk) * 3 + tj * PANE_WIDTH * 3 + 2] = Palette64[idx * 3 + 2];
          mask <<= 1;
        }
      }
    }
    fillpannel();
  }
}
