enum eCommandType {
    nand_op_cmd_instr,
    nand_op_addr_instr,
    nand_op_data_in_instr,
    nand_op_data_out_instr,
    nand_op_waitrdy_instr
}

type tContextBuffer = ( input: seq[int], out: seq[int] );
type tContextData = ( buf: tContextBuffer, len: int );
type tContextWaitReady = ( timeout_ms: int );
type tContextCommand = ( opcode: int);
type tContextAddr = ( naddrs: int, addrs: seq[int] );
type tCommandContext = ( cmd: tContextCommand, addr: tContextAddr, dat: tContextData, waitrdy: tContextWaitReady );
type tOpInstr = ( cmdType: eCommandType, ctx: tCommandContext );

type tOpReq = ( commands: seq[tOpInstr] );
type tOpResp = (respCode: int, buffers: seq[seq[int]]);

event eOpReq: tOpReq;
event eOpResp: tOpResp;

fun commandIntToEnum(val: int) : eCommand {
    if (val == 1) {
        return c_read_setup;
    } else if (val == 2) {
        return c_read_execute;
    } else if (val == 3) {
        return c_program_setup;
    } else if (val == 4) {
        return c_program_execute;
    } else if (val == 5) {
        return c_erase_setup;
    } else if (val == 6) {
        return c_erase_execute;
    } else {
        return c_dummy;
    }
}
machine Foxtrot0 {
    var nandDevice: Nand;
    var kernelTimeout : ReliableTimer;
    var client : machine;
    var status: int;
    var command: eCommand;
    var address: int;
    var val: int;
    var timedOut: bool;
    var readBuff: seq[int];
    var numToRead: int;
    var numRead: int;
    var currOpReq: tOpReq;
    var currOp: int;
    var err: int;
    var buffers: seq[seq[int]];

    start state Init {
        entry (dev: Nand) {
            nandDevice = dev;
            kernelTimeout = CreateReliableTimer(this);
            status = 0;
            command = c_dummy;
            address = 0;
            val = 0;
        }

        on eRegisterClientResp do {
            send client, eRegisterClientResp;
            goto AwaitingCommand;
        }

        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;
            send nandDevice, eRegisterClient, this;
        }
    }

    state AwaitingCommand {
        on eOpReq do (req: tOpReq) {
            var newBuffers: seq[seq[int]];
            currOpReq = req;
            currOp = 0;
            buffers = newBuffers;
            goto PerformingCommand;
        }
    }

    state PerformingCommand {
        entry {
            var cmd: tOpInstr;
            var newBuffer: seq[int];
            var numWritten: int;
            var i: int;

            if (currOp >= sizeof(currOpReq.commands)) {
                send client, eOpResp, (respCode=err, buffers=buffers);
                goto AwaitingCommand;
            }
            cmd = currOpReq.commands[currOp];

            if (cmd.cmdType == nand_op_cmd_instr) {
                command = commandIntToEnum(cmd.ctx.cmd.opcode);
                send nandDevice, eIORegisterReadWrite, (status=status, command=command, address=address, val=val, write=true);
                err = 0;
                goto NextCommand;
            } else if (cmd.cmdType == nand_op_addr_instr) {
                i = 0;
                while (i < cmd.ctx.addr.naddrs) {
                    address = cmd.ctx.addr.addrs[i];
                    send nandDevice, eIORegisterReadWrite, (status=status, command=command, address=address, val=val, write=true);
                    i = i + 1;
                }
                err = 0;
                goto NextCommand;
            } else if (cmd.cmdType == nand_op_data_in_instr) {
                if (cmd.ctx.dat.len <= 0) {
                    err = 0;
                    goto NextCommand;
                }
                numWritten = 0;
                if (cmd.ctx.dat.len < 1) {
                    goto AwaitingCommand;
                }
                    
                while (numWritten < cmd.ctx.dat.len) {
                    send nandDevice, eIORegisterReadWrite, (status=status, command=c_program_setup, address=address, val=cmd.ctx.dat.buf.out[numWritten], write=true);
                    numWritten = numWritten + 1;
                }
                err = 0;
                goto NextCommand;
            } else if (cmd.cmdType == nand_op_data_out_instr) {
                readBuff = newBuffer;
                numToRead = cmd.ctx.dat.len;
                numRead = 0;
                send nandDevice, eIORegisterReadWrite, (status=status,command=command,address=address,val=val,write=false);
                goto Reading;
            } else if (cmd.cmdType == nand_op_waitrdy_instr) {
                send nandDevice, eGPIOGetStatus;
                goto Waiting;
            }
        }
    }
            
    state NextCommand {
        entry {
            currOp = currOp + 1;
            if (currOp >= sizeof(currOpReq.commands)) {
                send client, eOpResp, (respCode=err, buffers=buffers);
                goto AwaitingCommand;
            }
            goto PerformingCommand;
        }
    }
    state Waiting {
        on eGPIOStatus do (ready: bool) {
            if (ready) {
                err = 0;
                goto NextCommand;
            }
            send nandDevice, eGPIOGetStatus;
        }

        on eReliableTimerStarted do {
            timedOut = false;
        }

        on eReliableTimeOut do {
            timedOut = true;
            err = -1;
            goto NextCommand;
        }
    }

    state Reading {
        on eIORegister do (req: tIORegister) {
            var readResp: tReadResp;
            var regRead: tIORegisterReadWrite;
            readBuff += (numRead, req.val);
            numRead = numRead + 1;
            if (numRead >= numToRead) {
                buffers += (sizeof(buffers), readBuff);
                err = 0;
                goto NextCommand;
            } else {
                regRead = (status=status, command=command, address=address, val=val, write=false);
                send nandDevice, eIORegisterReadWrite, regRead;
            }
        }

        on eReliableTimerStarted do {
            timedOut = false;
        }

        on eReliableTimeOut do {
            timedOut = true;
            err = -1;
            goto NextCommand;
        }
    }
}   
