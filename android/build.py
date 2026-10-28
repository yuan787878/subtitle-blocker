"""Build a locally signed universal Android APK using official build tools.
Requires Python 3 and a JDK (java, javac, jar, keytool on PATH).
The downloaded SDK stays in this project; nothing is installed system-wide.
"""
from pathlib import Path
import hashlib, os, shutil, subprocess, sys, urllib.request, zipfile
from concurrent.futures import ThreadPoolExecutor

ROOT=Path(__file__).resolve().parent
WORK=ROOT/'work'
SDK=WORK/'sdk'
MAIN=ROOT/'app'/'src'/'main'
OUT=ROOT/'安装包'
PACKAGES=[('platform-35_r02.zip','0bb560a90a7a2cbd0dd8348224d518b638fe7949'),('build-tools_r35_windows.zip','af059bb67cf7786f45ee0db85e2d24985df1b4b6')]
def download(pair):
    name,digest=pair
    file=WORK/name
    if not file.exists(): urllib.request.urlretrieve('https://dl.google.com/android/repository/'+name,file)
    if hashlib.sha1(file.read_bytes()).hexdigest()!=digest: raise RuntimeError('Checksum mismatch: '+name)
    with zipfile.ZipFile(file) as archive:
        for member in archive.infolist():
            if SDK.resolve() not in (SDK/member.filename).resolve().parents: raise RuntimeError('Invalid ZIP path')
        archive.extractall(SDK)
def run(*args):
    command=[]
    for index,arg in enumerate(args):
        if index and isinstance(arg,Path):
            try: arg=arg.relative_to(ROOT)
            except ValueError: pass
        command.append(str(arg))
    subprocess.run(command,check=True,cwd=ROOT)
def main():
    WORK.mkdir(exist_ok=True);SDK.mkdir(exist_ok=True);OUT.mkdir(exist_ok=True)
    if not (SDK/'android-35'/'android.jar').exists() or not (SDK/'android-15'/'aapt2.exe').exists():
        with ThreadPoolExecutor(max_workers=2) as pool: list(pool.map(download,PACKAGES))
    tools=SDK/'android-15';android=SDK/'android-35'/'android.jar'
    java=shutil.which('java');javac=shutil.which('javac');jar=shutil.which('jar');keytool=shutil.which('keytool')
    if not all([java,javac,jar,keytool]): raise RuntimeError('A JDK is required on PATH')
    classes=WORK/'classes';generated=WORK/'generated';dex=WORK/'dex'
    # Only these derived build directories are replaced; preserve SDK and signing key.
    for folder in [classes,generated,dex]:
        resolved=folder.resolve()
        if WORK.resolve() not in resolved.parents: raise RuntimeError('Unsafe build directory')
        if folder.exists(): shutil.rmtree(folder)
        folder.mkdir()
    run(tools/'aapt2.exe','compile','--dir',MAIN/'res','-o',WORK/'resources.zip')
    unsigned=WORK/'unsigned.apk'
    run(tools/'aapt2.exe','link','-o',unsigned,'-I',android,'--manifest',MAIN/'AndroidManifest.xml','--java',generated,'--min-sdk-version','26','--target-sdk-version','35',WORK/'resources.zip')
    sources=sorted((MAIN/'java').rglob('*.java'))+sorted(generated.rglob('*.java'))
    run(javac,'--release','8','-encoding','UTF-8','-classpath',android,'-d',classes,*sources)
    run(jar,'cf',WORK/'classes.jar','-C',classes,'.')
    run(java,'-cp',tools/'lib'/'d8.jar','com.android.tools.r8.D8','--release','--min-api','26','--lib',android,'--output',dex,WORK/'classes.jar')
    with zipfile.ZipFile(unsigned,'a',zipfile.ZIP_DEFLATED) as package:
        for file in dex.glob('*.dex'):package.write(file,file.name)
    aligned=WORK/'aligned.apk'
    run(tools/'zipalign.exe','-f','4',unsigned,aligned)
    key=WORK/'local-signing.p12'
    if not key.exists():
        run(keytool,'-genkeypair','-keystore',key,'-storetype','PKCS12','-storepass','android','-keypass','android','-alias','subtitleblur','-keyalg','RSA','-keysize','2048','-validity','10000','-dname','CN=Subtitle Blur Local Build','-noprompt')
    signed=WORK/'signed.apk'
    run(java,'-jar',tools/'lib'/'apksigner.jar','sign','--ks',key,'--ks-key-alias','subtitleblur','--ks-pass','pass:android','--key-pass','pass:android','--out',signed,aligned)
    run(java,'-jar',tools/'lib'/'apksigner.jar','verify','--verbose',signed)
    run(tools/'zipalign.exe','-c','4',signed)
    run(tools/'aapt2.exe','dump','badging',signed)
    output=OUT/'字幕遮挡器-Android.apk'
    shutil.copyfile(signed,output)
    (OUT/'SHA256.txt').write_text(hashlib.sha256(output.read_bytes()).hexdigest()+'  '+output.name+'\n',encoding='utf-8')
    print('APK:',output,flush=True)
if __name__=='__main__':main()
