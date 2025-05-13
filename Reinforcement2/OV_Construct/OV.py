# -*- coding: utf-8 -*-
"""
Created on Fri May  2 17:05:44 2025

@author: Pchelovod
"""

import json


import requests




import codecs

path_export = r"D:\образование\Ревит"+"\\"
path_export = r"C:\Users\KVinogradov\Desktop\сборки"+"\\"

Dict_Axis=json.load(codecs.open(path_export+'json_Dict_Axis.json'.replace("\\","/"), 'r', 'utf-8-sig'))

Dict_level_ventsId =json.load(codecs.open(path_export+'json_Dict_level_ventsId.json'.replace("\\","/"), 'r', 'utf-8-sig'))

Dict_ventId_Properts = json.load(codecs.open(path_export+'json_Dict_ventId_Properts.json'.replace("\\","/"), 'r', 'utf-8-sig'))

Dict_Grup_numOV_spisokOV = json.load(codecs.open(path_export+'json_Dict_Grup_numOV_spisokOV.json'.replace("\\","/"), 'r', 'utf-8-sig'))


List_Size_OV = json.load(codecs.open(path_export+'json_List_Size_OV.json'.replace("\\","/"), 'r', 'utf-8-sig'))

Dict_numOV_nearAxes = json.load(codecs.open(path_export+'json_Dict_numOV_nearAxes.json'.replace("\\","/"), 'r', 'utf-8-sig'))

Dict_numerateOV =json.load(codecs.open(path_export+'json_Dict_numerateOV.json'.replace("\\","/"), 'r', 'utf-8-sig'))


Dict_sovpad_level =json.load(codecs.open(path_export+'json_Dict_sovpad_level.json'.replace("\\","/"), 'r', 'utf-8-sig'))