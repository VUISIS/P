enum eCommand {
    c_read_setup,
    c_read_execute,
    c_program_setup,
    c_program_execute,
    c_erase_setup,
    c_erase_execute,
    c_dummy,
    gpio_get_status,
    gpio_reset
}

type tIORegisterReadWrite = (status: int, command: eCommand, address: int, val: int);
type tIORegister = (status: int, command: eCommand, address: int, val: int);
type tGPIOStatus = bool;

event eIORegisterReadWrite : tIORegisterReadWrite;
event eIORegister : tIORegister;
event eGPIOStatus : tGPIOStatus;
event eBugState;

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

    var cache: map[int,int];
    var blocks: map[int,map[int,map[int,int]]];

    fun reachedDeadline() : bool {
        if ($) {
            ready = true;
            return true;
        } else {
            return false;
        }
    }

    fun resetTimer() {
        ready = false;
    }
    
    start state Init {
        entry (clientRef : machine) {
            client = clientRef;
            ready = true;
            clearCursor();
            status = 0;
            command = c_dummy;
            address = 0;
            val = 0;
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
            if (req.command == c_read_setup) {
                sendRegister(client);
                goto s_read_awaiting_block_address;
            }
            else if (req.command == c_program_setup) {
                sendRegister(client);
                goto s_program_awaiting_block_address;
            }
            else if (req.command == c_erase_setup) {
                sendRegister(client);
                goto s_erase_awaiting_block_address;
            }
            else if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else {
                sendRegister(client);
                fail();
            }
        }
    
    }

    state s_bug {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_reset) {
                goto Init;
            }
            sendRegister(client);
            goto s_bug;
        }
    }

    state s_read_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_read_setup) {
                sendRegister(client);
                fail();
            } else {
                blockAddress = req.address;
                sendRegister(client);
                goto s_read_awaiting_page_address;
            }
        }
    }

    state s_read_awaiting_page_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_read_setup) {
                sendRegister(client);
                fail();
            } else {
                pageAddress = req.address;
                sendRegister(client);
                goto s_read_awaiting_byte_address;
            }
        }
    }

    state s_read_awaiting_byte_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_read_setup) {
                sendRegister(client);
                fail();
            } else {
                byteAddress = req.address;
                sendRegister(client);
                goto s_read_awaiting_execute;
            }
        }
    }


    state s_read_awaiting_execute {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_read_execute) {
                fail();
            } else {
                goto s_read_providing_data;
            }
        }
    }


    state s_read_providing_data {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (req.command == c_read_setup) {
                clearCursor();
                ready = true;
                sendStatus(client);
                goto s_read_awaiting_block_address;
            }
            else if (req.command == c_program_setup) {
                clearCursor();
                ready = true;
                sendStatus(client);
                goto s_program_awaiting_block_address;
            }
            else if (req.command == c_erase_setup) {
                clearCursor();
                ready = true;
                sendStatus(client);
                goto s_erase_awaiting_block_address;
            }
            else if (!reachedDeadline() || req.command != c_read_execute) {
                fail();
            } else {
                val = getFromMemory();
                sendStatus(client);
                stepAddress();
                resetTimer();
            }
        }
    }


    state s_program_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_program_setup) {
                sendRegister(client);
                fail();
            } else {
                blockAddress = req.address;
                sendRegister(client);
                goto s_program_awaiting_page_address;
            }
        }
    }


    state s_program_awaiting_page_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_program_setup) {
                sendRegister(client);
                fail();
            } else {
                pageAddress = req.address;
                sendRegister(client);
                goto s_program_awaiting_byte_address;
            }
        }
    }

    state s_program_awaiting_byte_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_program_setup) {
                sendRegister(client);
                fail();
            } else {
                byteAddress = req.address;
                sendRegister(client);
                goto s_program_accepting_data;
            }
        }
    }

    state s_program_accepting_data {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            var i : int;
            var addr : int;
            var addrs : seq[int];
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (req.command == c_read_setup) {
                clearCursor();
                ready = true;
                sendStatus(client);
                goto s_read_awaiting_block_address;
            }
            else if (req.command == c_program_setup) {
                cache[byteAddress] = req.val;
                byteAddress = byteAddress + 1;
                if (byteAddress >= 256) {
                    byteAddress = 0;
                }
                sendRegister(client);
                resetTimer();
            }
            else if (req.command == c_erase_setup) {
                clearCursor();
                ready = true;
                sendStatus(client);
                goto s_erase_awaiting_block_address;
            }
            else if (!reachedDeadline() || req.command != c_program_execute) {
                fail();
            } else {
                i = 0;
                addrs = keys(cache);
                while (i < sizeof(addrs)) {
                    setInMemory(addr, cache[addrs[i]]);
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
            }
        }
    }

    state s_erase_awaiting_block_address {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_erase_setup) {
                sendRegister(client);
                fail();
            } else {
                blockAddress = req.address;
                sendRegister(client);
                goto s_erase_awaiting_execute;
            }
        }
    }


    state s_erase_awaiting_execute {
        on eIORegisterReadWrite do (req: tIORegisterReadWrite) {
            var newBlock : map[int,map[int,int]];
            if (req.command == gpio_get_status) {
                sendStatus(client);
            }
            else if (req.command == gpio_reset) {
                goto Init;
            }
            else if (!reachedDeadline() || req.command != c_erase_execute) {
                fail();
            } else {
                blocks[blockAddress] = newBlock;
                sendRegister(client);
            }
        }
    }
}
