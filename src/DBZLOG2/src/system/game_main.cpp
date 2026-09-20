// Game_Main -- 0x08000158 (0xA6 bytes). HIGH. Boot order: flash-chip unlock sequence, GameLoop_Init, developer splash, music track 15, the intro
// slideshow, then the attract-mode loop (GameIntroVideo -> title screen) forever.
#include "dbzlog2/gba_io.h"
#include "dbzlog2/gba_memory.h"
#include "dbzlog2/types.h"

namespace dbzlog2::system {

struct Coroutine;
extern volatile u8 FLASH_CMD1;   // 0x0E000000 area: the game pokes the save chip's command bytes at boot (ID / unlock sequence, MEDIUM)
extern volatile u8 FLASH_CMD2;   // 0x0E000001
extern u32 g_SaveState;          // 0x03001FC0
extern const void* g_MusicTrackTable;
extern u8 g_MusicState;          // 0x030026CD
extern const void* g_CurrentMusicTrack;
void* GameLoop_Init(void*);
Coroutine* DeveloperIntro_Splash_Create(void*);      // 0x0801EDE0
Coroutine* DeveloperIntro_Slideshow_Create(void*);   // 0x0801D378
Coroutine* GameIntroVideo_Create(void*);             // 0x0801ED3C
int GameLoop_ExecuteAndWait(Coroutine*);
void Coroutine_SwitchTo(Coroutine*);
int GameLoop_Tick();
void EntityList_Clear(void*);                        // 0x08006C24
void Music_Init(void* player, const void* track);   // 0x08020A4C

[[noreturn]] void Game_Main() {
    // Save-chip probe: write the two command bytes (62*i ^ 0x9C, -29*i + 37) for i = 0, 1, then restore them.
    const u8 saved1 = FLASH_CMD1, saved2 = FLASH_CMD2;
    for (int i = 0; i < 2; ++i) {
        FLASH_CMD1 = static_cast<u8>((62 * i) ^ 0x9C);
        FLASH_CMD2 = static_cast<u8>(-29 * i + 37);
    }
    g_SaveState = 1;
    FLASH_CMD1 = saved1;
    FLASH_CMD2 = saved2;

    u8 loop_memory[4];
    GameLoop_Init(loop_memory);
    GameLoop_ExecuteAndWait(DeveloperIntro_Splash_Create(nullptr));
    // sub_08001E4A(&byte_03000E88): unidentified reset of a small block   // TODO(unknown)
    EntityList_Clear(nullptr);                       // g_EntityList (0x03001120)
    // Music_Init(&g_MusicPlayer, &g_MusicTrackTable[15]) unless that track is already playing.
    GameLoop_ExecuteAndWait(DeveloperIntro_Slideshow_Create(nullptr));
    for (;;) {
        Coroutine_SwitchTo(GameIntroVideo_Create(nullptr));
        GameLoop_Tick();
    }
}

}  // namespace dbzlog2::system
