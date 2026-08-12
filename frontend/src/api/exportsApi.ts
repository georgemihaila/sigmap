import { api } from './baseApi';
import type { ExportFormat, ExportRecord } from '@/lib/domain';

export interface ExportInput {
  sessionId?: string | null;
  format: ExportFormat;
  /** Optional date-range filter for exports. */
  from?: string | null;
  to?: string | null;
}

export const exportsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listExports: builder.query<ExportRecord[], void>({
      query: () => ({ url: '/exports' }),
    }),
    createExport: builder.mutation<
      { id: string; format: ExportFormat; status: string; createdAt: string; fileName: string | null },
      ExportInput
    >({
      query: (body) => ({ url: '/exports', method: 'POST', body }),
      invalidatesTags: ['Export'],
    }),
    downloadExport: builder.query<string, string>({
      query: (id) => ({
        url: `/exports/${id}/download`,
        responseHandler: (response) => response.text(),
      }),
    }),
    uploadWigle: builder.mutation<{ status: string; message: string | null }, { sessionId: string }>({
      query: (body) => ({ url: '/wigle/upload', method: 'POST', body }),
      invalidatesTags: ['Detected'],
    }),
    importWigle: builder.mutation<{ imported: number }, void>({
      query: () => ({ url: '/wigle/import', method: 'POST' }),
      invalidatesTags: ['Detected'],
    }),
    getWigleSettings: builder.query<{
      apiName: string | null;
      apiKeySet: boolean;
      username: string | null;
      passwordSet: boolean;
    } | null, void>({
      query: () => ({ url: '/settings/wigle' }),
    }),
    putWigleSettings: builder.mutation<
      { apiName: string | null; apiKeySet: boolean; username: string | null; passwordSet: boolean },
      { apiName?: string; apiKey?: string; username?: string; password?: string }
    >({
      query: (body) => ({ url: '/settings/wigle', method: 'PUT', body }),
    }),
  }),
});

export const {
  useListExportsQuery,
  useCreateExportMutation,
  useDownloadExportQuery,
  useLazyDownloadExportQuery,
  useUploadWigleMutation,
  useImportWigleMutation,
  useGetWigleSettingsQuery,
  usePutWigleSettingsMutation,
} = exportsApi;
