export interface TrashFile {
  id: string;
  fileName: string;
  fileExtension: string;
  fileSizeInBytes: number;
  versionNumber: number;
  description: string;
  category: string;
  itemId: string;
  itemName: string;
  createdAt: string;
  createdBy: string;
  deletedAt: string;
  deletedBy: string;
  daysInTrash: number;
  daysRemaining: number;
  canRestore: boolean;
}

export interface TrashStats {
  totalTrashedFiles: number;
  filesExpiringSoon: number;
  totalSizeInBytes: number;
  oldestFileDate: string;
}
