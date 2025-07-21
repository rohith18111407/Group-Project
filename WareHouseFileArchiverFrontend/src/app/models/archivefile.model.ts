export interface ArchiveFile {
  id: string;
  fileName: string;
  fileExtension: string;
  fileSizeInBytes: number;
  filePath: string;
  versionNumber: number;
  description: string;
  category: string;
  itemId: string;
  itemName: string;
  createdAt: string;
  createdBy: string;
  updatedAt?: string;
  updatedBy?: string;
  isScheduled: boolean;
  isProcessed: boolean;
}
