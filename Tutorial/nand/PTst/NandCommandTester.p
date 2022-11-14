
machine NandCommandTester
{
    var client: machine;
    var driver: machine;
    var reading: bool;
    var blockAddress: int;
    var pageAddress: int;
    var byteAddress: int;
    var writeBuffer: seq[int];
    var bytesWritten: int;
    var bytes: int;

    fun new_buffer() : seq[int] {
        var buff : seq[int];
        return buff;
    }

    fun sendFailure() {
        var readResp: tReadTestResponse;
        var writeResp: tWriteTestResponse;
        var emptyBuff: seq[int];

        if (reading) {
            readResp = (buffer=emptyBuff, len=-1);
            send client, eReadTestResponse, readResp;
        } else {
            send client, eWriteTestResponse, -1;
        }
    }
            
    start state Init {
        entry (dr: tNandTesterInit) {
            driver = dr;

        }
        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;

            send driver, eRegisterClient, this;
        }

        on eRegisterClientResp do {
            send client, eRegisterClientResp;
            goto testloop;
        }
    }

    state testloop {
        on eReadTestRequest do (req: tReadTestRequest) {
            var opInstrs: seq[tOpInstr];
            var opInstr: tOpInstr;
            var addrs: seq[int];
            var emptyAddrs: tContextAddr;
            var emptyData: tContextData;
            var emptyBuff: tContextBuffer;
            var readData: tContextData;
            var emptyWait: tContextWaitReady;
            var commandContext: tCommandContext;
            var contextCommand: tContextCommand;

            contextCommand = (opcode=1,);
            commandContext=(cmd=contextCommand,addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait);
            opInstr = (cmdType=nand_op_cmd_instr, ctx=commandContext);
            opInstrs += (0, opInstr);
            addrs = new_buffer();
            addrs += (0, req.blockAddress);
            addrs += (1, req.pageAddress);
            addrs += (2, req.byteAddress);
            opInstr = (cmdType=nand_op_addr_instr, ctx=(cmd=(opcode=1,), addr=(naddrs=3,addrs=addrs),dat=emptyData,waitrdy=emptyWait));
            opInstrs += (1, opInstr);
            contextCommand = (opcode=2,);
            commandContext=(cmd=contextCommand,addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait);
            opInstr = (cmdType=nand_op_cmd_instr, ctx=commandContext);
            opInstrs += (2, opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,), addr=emptyAddrs, dat=emptyData, waitrdy=emptyWait));
            opInstrs += (3, opInstr);
            readData = (buf=emptyBuff, len=req.len);
            opInstr = (cmdType=nand_op_data_out_instr, ctx=(cmd=(opcode=1,), addr=emptyAddrs, dat=readData, waitrdy=emptyWait));
            opInstrs += (4, opInstr);
            send driver, eOpReq, (commands=opInstrs,);
            reading = true;
            goto awaitReply;
        }

        on eWriteTestRequest do (req: tWriteTestRequest) {
            var opInstrs: seq[tOpInstr];
            var opInstr: tOpInstr;
            var addrs: seq[int];
            var emptyAddrs: tContextAddr;
            var emptyData: tContextData;
            var emptyWait: tContextWaitReady;
            opInstr = (cmdType=nand_op_cmd_instr, ctx=(cmd=(opcode=3,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (0,opInstr);
            addrs = new_buffer();
            addrs += (0, req.blockAddress);
            addrs += (1, req.pageAddress);
            addrs += (2, req.byteAddress);
            opInstr = (cmdType=nand_op_addr_instr, ctx=(cmd=(opcode=0,),addr=(naddrs=3,addrs=addrs),dat=emptyData,waitrdy=emptyWait));
            opInstrs += (1,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (2,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (3,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (4,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (5,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (6,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (7,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (8,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (9,opInstr);
            opInstr = (cmdType=nand_op_data_in_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=(buf=(input=new_buffer(),out=req.buffer),len=req.len),waitrdy=emptyWait));
            opInstrs += (10,opInstr);
            opInstr = (cmdType=nand_op_cmd_instr, ctx=(cmd=(opcode=4,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (11,opInstr);
            opInstr = (cmdType=nand_op_waitrdy_instr, ctx=(cmd=(opcode=1,),addr=emptyAddrs,dat=emptyData,waitrdy=emptyWait));
            opInstrs += (12,opInstr);
            send driver, eOpReq, (commands=opInstrs,);
            reading = false;
            goto awaitReply;
        }
    }

    state awaitReply {
        on eOpResp do (resp: tOpResp) {
            var lastBuff: int;
            if (reading) {
                if (resp.respCode != 0) {
                    send client, eReadTestResponse, (buffer=new_buffer(),len=0);
                } else {
                    lastBuff = sizeof(resp.buffers)-1;
                    send client, eReadTestResponse, (buffer=resp.buffers[lastBuff], len=sizeof(resp.buffers[lastBuff]));
                }
            } else {
                if (resp.respCode != 0) {
                    send client, eWriteTestResponse, -1;
                } else {
                    send client, eWriteTestResponse, 0;
                }
            }
            goto testloop;
        }
    }
}
