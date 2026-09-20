// BytecodeVM_ReturnToTitleScreen -- action opcode 97, 0x0800A3E8 (0x26 bytes). HIGH.
// Marks the player as despawned, clears the entity list and trigger list, runs sub_0801F346 (unidentified reset) and switches to the intro/title coroutine (GameIntroVideo_Create).
#include "dbzlog2/vm/vm_globals.h"

namespace dbzlog2::vm {

void EntityList_Clear(void* list);        // 0x08006C24
void TriggerList_Init(void* list);        // 0x0800BF08
extern void* g_TriggerList;               // 0x03001FE0
void ResetAfterGame_801F346();            // 0x0801F346 (IDA: sub_801F346)  TODO(unknown)
void* GameIntroVideo_Create(void* memory);   // 0x0801ED3C
void Coroutine_SwitchTo(void* coroutine); // 0x0801F2CE

void BytecodeVM_ReturnToTitleScreen(VmContext*, const u8**) {
    g_PlayerWasDespawned = 1;
    EntityList_Clear(g_EntityList);
    TriggerList_Init(g_TriggerList);
    ResetAfterGame_801F346();
    Coroutine_SwitchTo(GameIntroVideo_Create(nullptr));
}

}  // namespace dbzlog2::vm
