"""
llama-cpp-python Wheel Downloader
Downloads the exact wheels used by MindVault's PythonBootstrapper.cs

Based on the URLs from:
- https://abetlen.github.io/llama-cpp-python/whl/
- GitHub releases: https://github.com/abetlen/llama-cpp-python/releases
"""

import os
import sys
import requests
from pathlib import Path

# Configuration
SOLUTION_DIR = Path(__file__).parent
WHEELS_DIR = SOLUTION_DIR / "Wheels"
WHEELS_DIR.mkdir(exist_ok=True)

# Wheel URLs - matching PythonBootstrapper.cs logic
WHEELS = {
    "cpu": {
        "url": "https://github.com/abetlen/llama-cpp-python/releases/download/v0.3.16/llama_cpp_python-0.3.16-cp311-cp311-win_amd64.whl",
        "filename": "llama_cpp_python-0.3.16-cp311-cp311-win_amd64.whl",
        "size_mb": 30,
        "description": "CPU version (works on all Windows PCs)"
    },
    "cuda122": {
        "url": "https://github.com/abetlen/llama-cpp-python/releases/download/v0.3.16/llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda122.whl",
        "filename": "llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda122.whl",
        "size_mb": 600,
        "description": "CUDA 12.2 version (for NVIDIA GPU)"
    },
    "cuda121": {
        "url": "https://github.com/abetlen/llama-cpp-python/releases/download/v0.3.16/llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda121.whl",
        "filename": "llama_cpp_python-0.3.16-cp311-cp311-win_amd64-cuda121.whl",
        "size_mb": 600,
        "description": "CUDA 12.1 version (for NVIDIA GPU)"
    }
}

# Fallback URLs from Abetlen's wheel repository (used by BuildLlamaInCmdAsync)
FALLBACK_URLS = [
    # These are tried via pip install with --extra-index-url in PythonBootstrapper
    "https://abetlen.github.io/llama-cpp-python/whl/",
    "https://abetlen.github.io/llama-cpp-python/whl/cu122",
    "https://abetlen.github.io/llama-cpp-python/whl/cu121",
]


def download_file(url: str, dest: Path, description: str, expected_size_mb: int) -> bool:
    """Download a file with progress indication."""
    
    if dest.exists():
        size_mb = dest.stat().st_size / (1024 * 1024)
        print(f"? Already exists: {dest.name} ({size_mb:.2f} MB)")
        return True
    
    print(f"\n?? Downloading: {description}")
    print(f"   URL: {url}")
    print(f"   Expected size: ~{expected_size_mb} MB")
    print(f"   Saving to: {dest}")
    
    try:
        response = requests.get(url, stream=True, timeout=30)
        response.raise_for_status()
        
        total_size = int(response.headers.get('content-length', 0))
        block_size = 8192
        downloaded = 0
        
        with open(dest, 'wb') as f:
            for chunk in response.iter_content(chunk_size=block_size):
                if chunk:
                    f.write(chunk)
                    downloaded += len(chunk)
                    
                    # Show progress every 10%
                    if total_size > 0:
                        progress = (downloaded / total_size) * 100
                        if int(progress) % 10 == 0:
                            print(f"   Progress: {progress:.0f}%", end='\r')
        
        size_mb = dest.stat().st_size / (1024 * 1024)
        print(f"\n   ? Downloaded successfully! ({size_mb:.2f} MB)")
        return True
        
    except requests.exceptions.RequestException as e:
        print(f"   ? Download failed: {e}")
        if dest.exists():
            dest.unlink()  # Remove partial download
        return False


def main():
    """Main download function."""
    
    print("=" * 60)
    print("  llama-cpp-python Wheel Downloader for MindVault")
    print("=" * 60)
    print(f"\nSolution directory: {SOLUTION_DIR}")
    print(f"Wheels directory: {WHEELS_DIR}")
    
    # Ask which wheels to download
    print("\n" + "=" * 60)
    print("Select wheels to download:")
    print("=" * 60)
    print("\n1. CPU only (required, ~30 MB)")
    print("2. CPU + CUDA 12.2 (recommended for NVIDIA GPU, ~630 MB total)")
    print("3. CPU + CUDA 12.1 (alternative NVIDIA GPU, ~630 MB total)")
    print("4. All wheels (CPU + both CUDA versions, ~1.2 GB total)")
    print("5. Skip download (just show URLs)")
    
    choice = input("\nEnter choice (1-5) [default: 1]: ").strip() or "1"
    
    to_download = []
    
    if choice == "1":
        to_download = ["cpu"]
    elif choice == "2":
        to_download = ["cpu", "cuda122"]
    elif choice == "3":
        to_download = ["cpu", "cuda121"]
    elif choice == "4":
        to_download = ["cpu", "cuda122", "cuda121"]
    elif choice == "5":
        print("\n" + "=" * 60)
        print("Wheel URLs (for manual download):")
        print("=" * 60)
        for key, info in WHEELS.items():
            print(f"\n{info['description']}:")
            print(f"  {info['url']}")
        print("\n" + "=" * 60)
        print("Fallback wheel repository URLs:")
        print("=" * 60)
        for url in FALLBACK_URLS:
            print(f"  {url}")
        return 0
    else:
        print("Invalid choice. Defaulting to CPU only.")
        to_download = ["cpu"]
    
    # Download selected wheels
    print("\n" + "=" * 60)
    print("Starting downloads...")
    print("=" * 60)
    
    success_count = 0
    fail_count = 0
    
    for wheel_key in to_download:
        wheel_info = WHEELS[wheel_key]
        dest_path = WHEELS_DIR / wheel_info["filename"]
        
        success = download_file(
            url=wheel_info["url"],
            dest=dest_path,
            description=wheel_info["description"],
            expected_size_mb=wheel_info["size_mb"]
        )
        
        if success:
            success_count += 1
        else:
            fail_count += 1
    
    # Summary
    print("\n" + "=" * 60)
    print("Download Summary")
    print("=" * 60)
    print(f"? Successful: {success_count}")
    print(f"? Failed: {fail_count}")
    
    # List downloaded files
    wheels = list(WHEELS_DIR.glob("*.whl"))
    if wheels:
        print("\n" + "=" * 60)
        print("Downloaded wheels:")
        print("=" * 60)
        for wheel in wheels:
            size_mb = wheel.stat().st_size / (1024 * 1024)
            print(f"  ? {wheel.name}")
            print(f"    Size: {size_mb:.2f} MB")
    
    # Show fallback info
    print("\n" + "=" * 60)
    print("Additional Information")
    print("=" * 60)
    print("\nYour PythonBootstrapper.cs also uses these fallback repositories:")
    for url in FALLBACK_URLS:
        print(f"  • {url}")
    
    print("\nNext steps:")
    print("  1. Rebuild your MindVault solution in Visual Studio")
    print("  2. Wheels will be bundled automatically")
    print("  3. Users won't need internet for AI setup!")
    
    return 0 if fail_count == 0 else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\n\nDownload cancelled by user.")
        sys.exit(1)
    except Exception as e:
        print(f"\n\nUnexpected error: {e}")
        sys.exit(1)
