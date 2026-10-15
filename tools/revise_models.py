from pathlib import Path
p=Path('Blender/build_clinic.py');s=p.read_text(encoding='utf-8')
a=s.index("# instrument tray for");b=s.index("cyl('StoolSeat'",a);s=s[:a]+s[b:]
a=s.index("box('MonitorBody'");b=s.index('# ================= Exam chair',a);s=s[:a]+s[b:]
a=s.index("cyl('StandBase',");b=s.index("cyl('StandColumn'",a)
s=s[:a]+'''box('LiftFoot',(1.08,.055,4.25),(.85,.10,.66),'Graphite',.045)
box('LiftPedestal',(1.08,.43,4.25),(.24,.75,.22),'StandWhite',.025)
box('LiftInnerColumn',(1.08,.72,4.25),(.18,.40,.16),'Chrome',.015)
deck=pivot('LiftDeck',(1.08,.92,4.25),group)
box('LiftTableTop',(1.08,.91,4.25),(1.06,.055,.88),'StandWhite',.035,deck)
box('LiftTableRim',(1.08,.881,4.25),(1.00,.025,.83),'Graphite',.025,deck)
box('MonitorBody',(1.16,1.14,4.59),(.46,.32,.045),'Graphite',.012,deck)
box('MonitorScreen',(1.16,1.14,4.562),(.42,.27,.009),'White',0,deck)
box('MonitorStem',(1.16,.99,4.60),(.035,.14,.035),'Chrome',.004,deck)
box('MonitorFoot',(1.16,.95,4.60),(.25,.018,.12),'Graphite',.008,deck)
''' +s[b:]
s=s.replace("(.62,1.05,4.425),.042,1.95", "(.62,1.48,4.425),.042,1.10")
a=s.index("group=root('Remote'");b=s.index('\n',s.index("for x in [-.012,.012]",a))
s=s[:a]+'''group=root('Remote',(0,0,0))
box('RemoteBody',(0,0,0),(.055,.185,.018),'Porcelain',.009)
box('RemoteFace',(0,.006,-.010),(.045,.151,.003),'Graphite',.002)
for row in range(9):
    for col in range(3):
        box('RemoteKey',(-.014+col*.014,.061-row*.014,-.013),(.010,.009,.004),'Red' if row==8 and col==1 else 'White',.001)
''' +s[b:]
p.write_text(s,encoding='utf-8')
