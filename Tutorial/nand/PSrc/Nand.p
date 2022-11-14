enum eCommand {
    c_zero,
    c_read_setup,
    c_read_execute,
    c_program_setup,
    c_program_execute,
    c_erase_setup,
    c_erase_execute,
    c_dummy
}

type tIORegisterReadWrite = (status: int, command: eCommand, address: int, val: int, write: bool);
type tIORegister = (status: int, command: eCommand, address: int, val: int);
type tGPIOStatus = bool;
type tRegisterClient = machine;

event eIORegisterReadWrite : tIORegisterReadWrite;
event eIORegister : tIORegister;
event eGPIOStatus : tGPIOStatus;
event eBugState;
event eGPIOGetStatus;
event eGPIOReset;
event eRegisterClient : tRegisterClient;
event eRegisterClientResp;

machine Nand
{
    var blockAddress: int;
    var pageAddress: int;
    var byteAddress: int;
    var currPage: int;
    var cursor: int;
    var status: int;
    var command: eCommand;
    var address: int;
    var val: int;
    var ready: bool;
    var client: machine;
    var timer: ReliableTimer;

    var cache: map[int,int];
    var blocks: map[int,map[int,map[int,int]]];

    fun ready() : bool {
        return ready;
    }

    fun resetTimer() {
        StartReliableTimer(timer);
        ready = false;
    }
    
    start state Init {
        entry {
            timer = CreateReliableTimer(this);
            ready = true;
            clearCursor();
            status = 0;
            command = c_dummy;
            address = 0;
            val = 0;
        }

        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;
            send client, eRegisterClientResp;
            goto s_initial_state;
        }
    }

    fun clearCursor() {
        blockAddress = 0;
        pageAddress = 0;
        byteAddress = 0;
    }

    fun sendRegister(client : machine) {
        var reg: tIORegister;

        reg = (status=status, command=command, address=address, val=val);
        send client, eIORegister, reg;
    }
    
    fun sendStatus(client : machine) {
        send client, eGPIOStatus, ready;
    }

    fun getFromMemory() : int {
        if (blockAddress in blocks) {
            if (pageAddress in blocks[blockAddress]) {
                if (byteAddress in blocks[blockAddress][pageAddress]) {
                    return blocks[blockAddress][pageAddress][byteAddress];
                }
            }
        }
        return 255;
    }

    fun fail() {
        send client,eBugState;
        goto s_bug;
    }

    fun setInMemory(byteAddress : int, val : int) {
        var newBlock : map[int,map[int,int]];
        var newPage : map[int,int];
        if (!(blockAddress in blocks)) {
            blocks[blockAddress] = newBlock;
        }
        if (!(pageAddress in blocks[blockAddress])) {
            blocks[blockAddress][pageAddress] = newPage;
        }
        blocks[blockAddress][pageAddress][byteAddress] = val;
    }

    fun stepAddress() {
        byteAddress = byteAddress + 1;
        if (byteAddress >= 256) {
            byteAddress = 0;
            pageAddress = pageAddress + 1;
            if (pageAddress >= 256) {
                pageAddress = 0;
                blockAddress = blockAddress + 1;
                if (blockAddress >= 256) {
                    blockAddress = 0;
                }
            }
        }
    }

    state s_initial_state {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (req.command == c_read_setup) {
                    clearCursor();
                    ready = true;
                    goto s_read_awaiting_block_address;
                }
                else if (req.command == c_program_setup) {
                    clearCursor();
                    ready = true;
                    goto s_program_awaiting_block_address;
                }
                else if (req.command == c_erase_setup) {
                    clearCursor();
                    ready = true;
                    goto s_erase_awaiting_block_address;
                }
                else if (req.command != c_dummy) {
                    fail();
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_bug {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            }
            goto s_bug;
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_read_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_read_setup) {
                    fail();
                } else {
                    blockAddress = req.address;
                    goto s_read_awaiting_page_address;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_read_awaiting_page_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_read_setup) {
                    fail();
                } else {
                    pageAddress = req.address;
                    goto s_read_awaiting_byte_address;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_read_awaiting_byte_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_read_setup) {
                    sendRegister(client);
                    fail();
                } else {
                    byteAddress = req.address;
                    goto s_read_awaiting_execute;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }


    state s_read_awaiting_execute {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_read_execute) {
                    fail();
                } else {
                    resetTimer();
                    goto s_read_providing_data;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }


    state s_read_providing_data {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                if (!ready()) {
                    fail();
                }
                val = getFromMemory();
                sendRegister(client);
                stepAddress();
            } else {
                if (req.command == c_read_setup) {
                    clearCursor();
                    ready = true;
                    goto s_read_awaiting_block_address;
                }
                else if (req.command == c_program_setup) {
                    clearCursor();
                    ready = true;
                    goto s_program_awaiting_block_address;
                }
                else if (req.command == c_erase_setup) {
                    clearCursor();
                    ready = true;
                    goto s_erase_awaiting_block_address;
                }
                else if (!ready() || req.command != c_read_execute) {
                    fail();
                } else {
                    fail();
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }


    state s_program_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_program_setup) {
                    fail();
                } else {
                    blockAddress = req.address;
                    goto s_program_awaiting_page_address;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }


    state s_program_awaiting_page_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_program_setup) {
                    fail();
                } else {
                    pageAddress = req.address;
                    goto s_program_awaiting_byte_address;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_program_awaiting_byte_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_program_setup) {
                    fail();
                } else {
                    byteAddress = req.address;
                    goto s_program_accepting_data;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_program_accepting_data {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            var i : int;
            var addr : int;
            var addrs : seq[int];
            if (!req.write) {
                sendRegister(client);
            } else {
                if (req.command == c_read_setup) {
                    clearCursor();
                    ready = true;
                    goto s_read_awaiting_block_address;
                }
                else if (req.command == c_program_setup) {
                    cache[byteAddress] = req.val;
                    byteAddress = byteAddress + 1;
                    if (byteAddress >= 256) {
                        byteAddress = 0;
                    }
                }
                else if (req.command == c_erase_setup) {
                    clearCursor();
                    ready = true;
                    goto s_erase_awaiting_block_address;
                }
                else if (!ready() || req.command != c_program_execute) {
                    fail();
                } else {
                    i = 0;
                    addrs = keys(cache);
                    while (i < sizeof(addrs)) {
                        setInMemory(addrs[i], cache[addrs[i]]);
                        i = i + 1;
                    }
                    pageAddress = pageAddress + 1;
                    if (pageAddress >= 256) {
                        pageAddress = 0;
                        blockAddress = blockAddress + 1;
                        if (blockAddress >= 256) {
                            blockAddress = 0;
                        }
                    }
                    resetTimer();
                    goto s_initial_state;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }

    state s_erase_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_erase_setup) {
                    fail();
                } else {
                    blockAddress = req.address;
                    goto s_erase_awaiting_execute;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }


    state s_erase_awaiting_execute {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            var newBlock : map[int,map[int,int]];
            if (!req.write) {
                sendRegister(client);
            } else {
                if (!ready() || req.command != c_erase_execute) {
                    fail();
                } else {
                    blocks[blockAddress] = newBlock;
                    resetTimer();
                    goto s_initial_state;
                }
            }
        }

        on eGPIOGetStatus do {
            sendStatus(client);
        }

        on eGPIOReset do {
            goto Init;
        }

        on eReliableTimerStarted do {
            ready = false;
        }
    
        on eReliableTimeOut do {
            ready = true;
        }
    }
}
