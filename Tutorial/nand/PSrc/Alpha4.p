
machine Alpha4 {
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
        on eSetNandRegister do (req: tSetNandRegister) {
            var regUpdate : tIORegisterReadWrite;
            if (req.offset == reg_status) {
                status = req.val;
            } else if (req.offset == reg_command) {
                if (req.val == 1) {
                    command = c_read_setup;
                } else if (req.val == 2) {
                    command = c_read_execute;
                } else if (req.val == 3) {
                    command = c_program_setup;
                } else if (req.val == 4) {
                    command = c_program_execute;
                } else if (req.val == 5) {
                    command = c_erase_setup;
                } else if (req.val == 6) {
                    command = c_erase_execute;
                } else if (req.val == 6) {
                    command = c_dummy;
                }
            } else if (req.offset == reg_address) {
                address = req.val;
            } else if (req.offset == reg_val) {
                val = req.val;
            }
            regUpdate = (status=status, command=command, address=address, val=val, write=true);
            send nandDevice, eIORegisterReadWrite, regUpdate;
        }

        on eWait do (req: tWait) {
            send nandDevice, eGPIOGetStatus;
            goto Waiting;
            
        }
        on eRead do (req: tRead) {
            var newBuffer: seq[int];
            var regRead: tIORegisterReadWrite;
            readBuff = newBuffer;
            numToRead = 8 * (req / 8);
            regRead = (status=status, command=command, address=address, val=val, write=false);
            send nandDevice, eIORegisterReadWrite, regRead;
            goto Reading;
        }

        on eProgram do (req: tProgram) {
            var regWrite: tIORegisterReadWrite;
            var programResp: tProgramResp;
            var numWritten: int;
            var numToWrite: int;
            numWritten = 0;
            if (req.len < 1) {
                goto AwaitingCommand;
            }
            numToWrite = 8 * (numToWrite / 8);
                
            while (numWritten < numToWrite) {
                regWrite = (status=status, command=c_program_setup, address=address, val=req.buffer[numWritten], write=true);
                send nandDevice, eIORegisterReadWrite, regWrite;
                numWritten = numWritten + 1;
            }
            programResp = numWritten;
            send client, eProgramResp, programResp;
            goto AwaitingCommand;
        }

        on eReliableTimerStarted do {
            timedOut = false;
        }

        on eReliableTimeOut do {
            timedOut = true;
        }

    }

    state Waiting {
        on eGPIOStatus do (ready: bool) {
            if (ready) {
                send client, eWaitResp, 0;
                goto AwaitingCommand;
            }
            send nandDevice, eGPIOGetStatus;
        }

        on eReliableTimerStarted do {
            timedOut = false;
        }

        on eReliableTimeOut do {
            timedOut = true;
            send client, eWaitResp, -1;
            goto AwaitingCommand;
        }
    }

    state Reading {
        on eIORegister do (req: tIORegister) {
            var readResp: tReadResp;
            var regRead: tIORegisterReadWrite;
            readBuff += (numRead, req.val);
            numRead = numRead + 1;
            if (numRead >= numToRead) {
                readResp = (buffer=readBuff, len=numRead);
                send client, eReadResp, readResp;
                goto AwaitingCommand;
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
        }
    }
}   
