using System;
using System.Runtime.InteropServices;

namespace LibuvSharp
{
	enum uv_process_flags : uint {
		UV_PROCESS_SETUID = (1 << 0),
		UV_PROCESS_SETGID = (1 << 1),
		UV_PROCESS_WINDOWS_VERBATIM_ARGUMENTS = (1 << 2),
		UV_PROCESS_DETACHED = (1 << 3)
	};

	enum uv_stdio_flags : int {
		UV_IGNORE         = 0x00,
		UV_CREATE_PIPE    = 0x01,
		UV_INHERIT_FD     = 0x02,
		UV_INHERIT_STREAM = 0x04,
		UV_READABLE_PIPE  = 0x10,
		UV_WRITABLE_PIPE  = 0x20
	}

	[StructLayout(LayoutKind.Sequential)]
	struct uv_stdio_container_stream_t {
		public uv_stdio_flags flags;
		public IntPtr stream;
	}

	[StructLayout(LayoutKind.Sequential)]
	unsafe struct uv_process_options_t : IDisposable
	{
		// fields

		public IntPtr exit_cb;
		public IntPtr file;
		public IntPtr args;
		public IntPtr env;
		public IntPtr cwd;

		public uint flags;

		public int uid;
		public int gid;

		public int stdio_count;
		uv_stdio_container_stream_t *stdio;

		// functions
		delegate void uv_exit_cb(IntPtr Handle, int exit_status, int term_signal);

		public uv_process_options_t(Process process, ProcessOptions options)
		{
			if (string.IsNullOrEmpty(options.File)) {
				throw new ArgumentException("file of processoptions can't be null");
			} else {
				file = Marshal.StringToHGlobalAnsi(options.File);
			}

			args = alloc(options.Arguments);
			env = alloc(options.Environment);
			cwd = Marshal.StringToHGlobalAnsi(options.CurrentWorkingDirectory);

			// all fields have to be set
			flags = 0;
			uid = 0;
			gid = 0;

			if (options.Detached) {
				flags |= (uint)uv_process_flags.UV_PROCESS_DETACHED;
			}

			if (options.WindowsVerbatimArguments) {
				flags |= (uint)uv_process_flags.UV_PROCESS_WINDOWS_VERBATIM_ARGUMENTS;
			}

			if (options.UID.HasValue) {
				flags |= (uint)uv_process_flags.UV_PROCESS_SETUID;
				uid = options.GID.Value;
			}

			if (options.GID.HasValue) {
				flags |= (uint)uv_process_flags.UV_PROCESS_SETGID;
				gid = options.GID.Value;
			}

			stdio_count = 3;
			stdio = (uv_stdio_container_stream_t *)Marshal.AllocHGlobal(stdio_count * sizeof(uv_stdio_container_stream_t));

			int i;
			for (i = 0; i < stdio_count; i++) {
				stdio[i].flags = 0;
			}

			foreach (var stream in new UVStream[] { options.Stdin, options.Stdout, options.Stderr }) {
				if (stream != null && stream is UVStream) {
					stdio[i].flags |= uv_stdio_flags.UV_INHERIT_STREAM;
					stdio[i].stream = stream.NativeHandle;
				}
				i++;
			}

			process.Data = GCHandle.ToIntPtr(process.GCHandle);
			exit_cb = Marshal.GetFunctionPointerForDelegate(cb);
		}

		static uv_exit_cb cb = exit;

		static void exit(IntPtr handle, int exit_status, int term_signal)
		{
			uv_handle_t *h = (uv_handle_t *)handle;
			var gchandle = GCHandle.FromIntPtr(h->data);
			var process = (gchandle.Target as Process);
			gchandle.Free();
			process.OnExit(exit_status, term_signal);
		}

		public void Dispose()
		{
			if (file != IntPtr.Zero) {
				Marshal.FreeHGlobal(file);
				file = IntPtr.Zero;
			}

			free(ref args);
			free(ref env);

			if (stdio != null) {
				Marshal.FreeHGlobal((IntPtr)stdio);
				stdio = null;
			}

			if (cwd != IntPtr.Zero) {
				Marshal.FreeHGlobal(file);
				cwd = IntPtr.Zero;
			}
		}

		static IntPtr alloc(string[] args)
		{
			if (args == null) {
				return IntPtr.Zero;
			}

			var arr = Marshal.AllocHGlobal((args.Length + 1) * sizeof(IntPtr));
			for (int i = 0; i < args.Length; i++) {
				Marshal.WriteIntPtr(arr, i * sizeof(IntPtr), Marshal.StringToHGlobalAnsi(args[i]));
			}
			Marshal.WriteIntPtr(arr, args.Length * sizeof(IntPtr), IntPtr.Zero);
			return arr;
		}

		static void free(ref IntPtr ptr)
		{
			if (ptr == IntPtr.Zero) {
				return;
			}

			int i = 0;
			IntPtr p = Marshal.ReadIntPtr(ptr);
			while (p != IntPtr.Zero) {
				Marshal.FreeHGlobal(p);
				i++;
				p = Marshal.ReadIntPtr(ptr, i * sizeof(IntPtr));
			}

			Marshal.FreeHGlobal(ptr);
			ptr = IntPtr.Zero;
		}
	}
}

